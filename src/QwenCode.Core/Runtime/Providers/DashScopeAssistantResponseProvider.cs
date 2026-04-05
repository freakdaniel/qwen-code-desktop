using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Telemetry;

namespace QwenCode.App.Runtime;

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
            string.IsNullOrWhiteSpace(request.SystemPromptOverride) ? options.SystemPrompt : request.SystemPromptOverride,
            request,
            promptContext,
            toolHistory,
            metadata,
            configuration.ExtraBody);

        var preferStreaming = true;
        var allowStreamingRecovery = true;

        while (true)
        {
            payload["stream"] = preferStreaming;
            using var response = await SendRequestAsync(
                configuration,
                payload,
                request,
                preferStreaming,
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
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, configuration.Endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiKey);
        foreach (var header in configuration.Headers)
        {
            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload),
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

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            return null;
        }

        return response;
    }
}
