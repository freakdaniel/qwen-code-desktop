using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Options;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class DashScopeAssistantResponseProvider(
    HttpClient httpClient,
    ProviderConfigurationResolver configurationResolver) : IAssistantResponseProvider
{
    public string Name => "qwen-compatible";

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

        var payload = OpenAiCompatibleProtocol.BuildPayload(
            configuration.Model,
            options.Temperature,
            options.SystemPrompt,
            request,
            promptContext,
            toolHistory,
            metadata,
            configuration.ExtraBody);
        payload["stream"] = true;

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

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        if (response.Content.Headers.ContentType?.MediaType?.Contains("event-stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await ReadStreamingResponseAsync(
                response,
                configuration.Model,
                eventSink,
                cancellationToken);
        }

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

    private async Task<AssistantTurnResponse?> ReadStreamingResponseAsync(
        HttpResponseMessage response,
        string model,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var summaryBuilder = new StringBuilder();
        var toolCalls = new Dictionary<int, StreamingToolCallState>();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            ProcessStreamingChunk(payload, summaryBuilder, toolCalls, eventSink);
        }

        var resolvedToolCalls = toolCalls
            .OrderBy(static pair => pair.Key)
            .Select(static pair => pair.Value.ToAssistantToolCall())
            .Where(static item => item is not null)
            .Cast<AssistantToolCall>()
            .ToArray();
        var summary = summaryBuilder.ToString().Trim();

        if (resolvedToolCalls.Length == 0 && string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        return new AssistantTurnResponse
        {
            Summary = summary,
            ProviderName = Name,
            Model = model,
            ToolCalls = resolvedToolCalls
        };
    }

    private static void ProcessStreamingChunk(
        string payload,
        StringBuilder summaryBuilder,
        IDictionary<int, StreamingToolCallState> toolCalls,
        Action<AssistantRuntimeEvent>? eventSink)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var contentDelta = ReadContentDelta(delta);
            if (!string.IsNullOrWhiteSpace(contentDelta))
            {
                summaryBuilder.Append(contentDelta);
                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "response-delta",
                    Message = contentDelta,
                    Status = "streaming",
                    ContentDelta = contentDelta,
                    ContentSnapshot = summaryBuilder.ToString()
                });
            }

            if (delta.TryGetProperty("tool_calls", out var toolCallsDelta) &&
                toolCallsDelta.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolCallDelta in toolCallsDelta.EnumerateArray())
                {
                    var index = toolCallDelta.TryGetProperty("index", out var indexProperty) &&
                                indexProperty.ValueKind == JsonValueKind.Number
                        ? indexProperty.GetInt32()
                        : toolCalls.Count;
                    if (!toolCalls.TryGetValue(index, out var toolState))
                    {
                        toolState = new StreamingToolCallState();
                        toolCalls[index] = toolState;
                    }

                    if (toolCallDelta.TryGetProperty("id", out var idProperty) &&
                        idProperty.ValueKind == JsonValueKind.String)
                    {
                        toolState.Id = idProperty.GetString() ?? string.Empty;
                    }

                    if (!toolCallDelta.TryGetProperty("function", out var functionProperty) ||
                        functionProperty.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (functionProperty.TryGetProperty("name", out var nameProperty) &&
                        nameProperty.ValueKind == JsonValueKind.String)
                    {
                        toolState.Name = nameProperty.GetString() ?? string.Empty;
                    }

                    if (functionProperty.TryGetProperty("arguments", out var argumentsProperty) &&
                        argumentsProperty.ValueKind == JsonValueKind.String)
                    {
                        toolState.ArgumentsBuilder.Append(argumentsProperty.GetString());
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed streaming chunks and continue with remaining deltas.
        }
    }

    private static string ReadContentDelta(JsonElement delta)
    {
        if (!delta.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(
                string.Empty,
                content.EnumerateArray()
                    .Select(static item =>
                        item.ValueKind == JsonValueKind.Object &&
                        item.TryGetProperty("text", out var textProperty) &&
                        textProperty.ValueKind == JsonValueKind.String
                            ? textProperty.GetString()
                            : null)
                    .Where(static item => !string.IsNullOrWhiteSpace(item))),
            _ => string.Empty
        };
    }

    private sealed class StreamingToolCallState
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public StringBuilder ArgumentsBuilder { get; } = new();

        public AssistantToolCall? ToAssistantToolCall()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return null;
            }

            return new AssistantToolCall
            {
                Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
                ToolName = Name,
                ArgumentsJson = ArgumentsBuilder.Length == 0 ? "{}" : ArgumentsBuilder.ToString()
            };
        }
    }
}
