using QwenCode.Core.Telemetry;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Dash Scope Assistant Response Provider
/// </summary>
/// <param name="httpClient">The http client</param>
/// <param name="configurationResolver">The configuration resolver</param>
/// <param name="tokenLimitService">The token limit service</param>
/// <param name="telemetryService">The telemetry service</param>
public sealed class DashScopeAssistantResponseProvider(
    HttpClient httpClient,
    ProviderConfigurationResolver configurationResolver,
    ITokenLimitService tokenLimitService,
    ITelemetryService? telemetryService = null) : IAssistantResponseProvider
{
    /// <summary>
    /// Gets the name
    /// </summary>
    public string Name => "qwen-compatible";

    /// <summary>
    /// Attempts to generate async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="promptContext">The prompt context</param>
    /// <param name="toolHistory">The tool history</param>
    /// <param name="options">The options</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to assistant turn response?</returns>
    public async Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(options.Provider, Name, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var configuration = configurationResolver.Resolve(request, options);
        if (string.IsNullOrWhiteSpace(configuration.Endpoint) || string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            return null;
        }

        var metadata = new JsonObject
        {
            ["sessionId"] = request.SessionId,
            ["promptId"] = $"{request.SessionId}:{toolHistory.Count + 1}",
            ["channel"] = "desktop"
        };
        var tokenLimits = tokenLimitService.Resolve(configuration.Model, options);
        var payload = OpenAiCompatibleProtocol.BuildPayload(
            configuration.Model,
            options.Temperature,
            tokenLimits.OutputTokenLimit,
            options.SystemPrompt,
            request,
            promptContext,
            toolHistory,
            metadata,
            configuration.ExtraBody,
            configuration.ProviderFlavor);

        var preferStreaming = true;
        var allowStreamingRecovery = true;

        while (true)
        {
            payload["stream"] = preferStreaming;
            OpenAiCompatibleProtocol.NormalizePayloadForQwenCompatible(
                payload,
                preferStreaming,
                request.DisableTools);
            using var response = await SendRequestAsync(
                configuration,
                payload,
                request,
                preferStreaming,
                eventSink,
                cancellationToken);
            if (response is null)
            {
                return null;
            }

            var isStreamingResponse = response.Content.Headers.ContentType?.MediaType?.Contains("event-stream", StringComparison.OrdinalIgnoreCase) == true;
            if (!isStreamingResponse)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var providerResponse = OpenAiCompatibleProtocol.TryReadResponse(content);
                if (providerResponse is null)
                {
                    return null;
                }

                return new AssistantTurnResponse
                {
                    Summary = providerResponse.Summary,
                    ProviderName = Name,
                    Model = configuration.Model,
                    ToolCalls = providerResponse.ToolCalls
                };
            }

            var streamingResult = await OpenAiCompatibleStreamingReader.ReadAsync(
                response,
                Name,
                configuration.Model,
                eventSink,
                cancellationToken);
            if (streamingResult.ShouldRetryNonStreaming && preferStreaming && allowStreamingRecovery)
            {
                allowStreamingRecovery = false;
                preferStreaming = false;
                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "stream-retry",
                    ProviderName = Name,
                    Status = "retrying",
                    Message = streamingResult.RetryReason
                });

                continue;
            }

            return streamingResult.Response;
        }
    }

    private async Task<HttpResponseMessage?> SendRequestAsync(
        ResolvedProviderConfiguration configuration,
        JsonObject payload,
        AssistantTurnRequest request,
        bool preferStreaming,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var attempt = 0;
        var currentDelay = AssistantProviderRetryPolicy.FirstDelay;

        while (true)
        {
            attempt++;
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, configuration.Endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
            foreach (var header in configuration.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            httpRequest.Content = new StringContent(
                payloadJson,
                Encoding.UTF8,
                "application/json");

            var stopwatch = Stopwatch.StartNew();
            var response = await httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (telemetryService is not null)
            {
                var isStreaming = response.Content.Headers.ContentType?.MediaType?.Contains("event-stream", StringComparison.OrdinalIgnoreCase) == true;
                await telemetryService.TrackApiRequestAsync(
                    request.RuntimeProfile,
                    request.SessionId,
                    Name,
                    configuration.Model,
                    stopwatch.ElapsedMilliseconds,
                    response.IsSuccessStatusCode ? "success" : "error",
                    (int)response.StatusCode,
                    response.IsSuccessStatusCode ? null : response.ReasonPhrase,
                    preferStreaming || isStreaming,
                    cancellationToken);
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;
            var shouldRetry = attempt < AssistantProviderRetryPolicy.MaxAttempts &&
                              AssistantProviderRetryPolicy.ShouldRetry(configuration.AuthType, response.StatusCode, responseBody);
            var hasRetryAfter = AssistantProviderRetryPolicy.TryGetRetryAfterDelay(response.Headers, out var retryDelay);

            response.Dispose();

            if (!shouldRetry)
            {
                TryWriteFailureDiagnostics(configuration, payloadJson, responseBody, statusCode);
                throw new AssistantProviderRequestException(
                    Name,
                    configuration.Endpoint,
                    statusCode,
                    responseBody);
            }

            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "provider-retry",
                ProviderName = Name,
                Status = "retrying",
                Message = $"Provider returned HTTP {statusCode}. Retrying request (attempt {attempt + 1}/{AssistantProviderRetryPolicy.MaxAttempts})."
            });

            var delay = hasRetryAfter
                ? retryDelay
                : AssistantProviderRetryPolicy.ApplyBackoff(currentDelay);
            await Task.Delay(delay, cancellationToken);
            currentDelay = hasRetryAfter
                ? AssistantProviderRetryPolicy.FirstDelay
                : AssistantProviderRetryPolicy.GetNextDelay(currentDelay);
        }
    }

    private static void TryWriteFailureDiagnostics(
        ResolvedProviderConfiguration configuration,
        string payloadJson,
        string responseBody,
        int statusCode)
    {
        try
        {
            var diagnosticPath = Path.Combine(Path.GetTempPath(), "qwen-desktop-last-provider-error.json");
            var diagnostic = new JsonObject
            {
                ["provider"] = "qwen-compatible",
                ["timestampUtc"] = DateTime.UtcNow.ToString("O"),
                ["endpoint"] = configuration.Endpoint,
                ["model"] = configuration.Model,
                ["statusCode"] = statusCode,
                ["headers"] = new JsonObject(configuration.Headers.Select(pair => KeyValuePair.Create<string, JsonNode?>(pair.Key, pair.Value))),
                ["requestBody"] = JsonNode.Parse(payloadJson),
                ["responseBody"] = string.IsNullOrWhiteSpace(responseBody)
                    ? JsonValue.Create(string.Empty)
                    : JsonNode.Parse(responseBody) ?? JsonValue.Create(responseBody)
            };

            File.WriteAllText(
                diagnosticPath,
                diagnostic.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);
        }
        catch
        {
            // Diagnostics are best-effort only and must not hide the real provider failure.
        }
    }
}
