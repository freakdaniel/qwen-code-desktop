namespace QwenCode.Core.Runtime;

internal static class OpenAiCompatibleStreamingReader
{
    /// <summary>
    /// Reads async
    /// </summary>
    /// <param name="response">The response payload</param>
    /// <param name="providerName">The provider name</param>
    /// <param name="model">The model</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to streaming read result</returns>
    public static async Task<StreamingReadResult> ReadAsync(
        HttpResponseMessage response,
        string providerName,
        string model,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var summaryBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        var toolCallParser = new StreamingToolCallParser();
        string? finishReason = null;
        string? embeddedError = null;
        var sawAnyToolCallDelta = false;

        await foreach (var payload in ReadServerSentEventPayloadsAsync(reader, cancellationToken))
        {
            if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
            {
                break;
            }

            ProcessStreamingChunk(
                payload,
                summaryBuilder,
                thinkingBuilder,
                toolCallParser,
                eventSink,
                ref finishReason,
                ref embeddedError,
                ref sawAnyToolCallDelta);
        }

        var resolvedToolCalls = toolCallParser.GetCompletedToolCalls();
        var summary = summaryBuilder.ToString().Trim();
        if (string.Equals(finishReason, "error_finish", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingReadResult.Retry(
                $"Streaming response returned an embedded provider error: {embeddedError ?? summary ?? "unknown error"}");
        }

        if (toolCallParser.HasIncompleteToolCalls() && sawAnyToolCallDelta)
        {
            return StreamingReadResult.Retry(
                "Streaming response ended with an incomplete tool call payload and needs one non-stream retry.");
        }

        var thinkingSummary = thinkingBuilder.ToString().Trim();

        if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase) &&
            resolvedToolCalls.Count == 0 &&
            string.IsNullOrWhiteSpace(summary) &&
            string.IsNullOrWhiteSpace(thinkingSummary))
        {
            return StreamingReadResult.Retry(
                "Streaming response hit the provider output limit before a complete assistant result was assembled.");
        }

        if (resolvedToolCalls.Count == 0 &&
            string.IsNullOrWhiteSpace(summary) &&
            string.IsNullOrWhiteSpace(thinkingSummary))
        {
            return StreamingReadResult.Empty();
        }

        return StreamingReadResult.Completed(new AssistantTurnResponse
        {
            Summary = summary,
            ThinkingSummary = thinkingSummary,
            ProviderName = providerName,
            Model = model,
            ToolCalls = resolvedToolCalls
        });
    }

    private static void ProcessStreamingChunk(
        string payload,
        StringBuilder summaryBuilder,
        StringBuilder thinkingBuilder,
        StreamingToolCallParser toolCallParser,
        Action<AssistantRuntimeEvent>? eventSink,
        ref string? finishReason,
        ref string? embeddedError,
        ref bool sawAnyToolCallDelta)
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
            if (firstChoice.TryGetProperty("finish_reason", out var finishReasonProperty) &&
                finishReasonProperty.ValueKind == JsonValueKind.String)
            {
                finishReason = finishReasonProperty.GetString();
            }

            if (!firstChoice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var contentDelta = ReadContentDelta(delta);
            if (!string.IsNullOrWhiteSpace(contentDelta))
            {
                summaryBuilder.Append(contentDelta);
                if (string.Equals(finishReason, "error_finish", StringComparison.OrdinalIgnoreCase))
                {
                    embeddedError = string.Concat(embeddedError, contentDelta);
                }

                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "response-delta",
                    Message = contentDelta,
                    Status = "streaming",
                    ContentDelta = contentDelta,
                    ContentSnapshot = summaryBuilder.ToString()
                });
            }

            var thinkingDelta = ReadThinkingDelta(delta);
            if (!string.IsNullOrWhiteSpace(thinkingDelta))
            {
                thinkingBuilder.Append(thinkingDelta);
                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "thinking-delta",
                    Message = thinkingDelta,
                    Status = "thinking",
                    ThinkingDelta = thinkingDelta,
                    ThinkingSnapshot = thinkingBuilder.ToString()
                });
            }

            if (delta.TryGetProperty("tool_calls", out var toolCallsDelta) &&
                toolCallsDelta.ValueKind == JsonValueKind.Array)
            {
                sawAnyToolCallDelta = true;
                foreach (var toolCallDelta in toolCallsDelta.EnumerateArray())
                {
                    var index = toolCallDelta.TryGetProperty("index", out var indexProperty) &&
                                indexProperty.ValueKind == JsonValueKind.Number
                        ? indexProperty.GetInt32()
                        : 0;

                    if (!toolCallDelta.TryGetProperty("function", out var functionProperty) ||
                        functionProperty.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var id = toolCallDelta.TryGetProperty("id", out var idProperty) &&
                             idProperty.ValueKind == JsonValueKind.String
                        ? idProperty.GetString()
                        : null;
                    var name = functionProperty.TryGetProperty("name", out var nameProperty) &&
                               nameProperty.ValueKind == JsonValueKind.String
                        ? nameProperty.GetString()
                        : null;
                    var argumentsChunk = functionProperty.TryGetProperty("arguments", out var argumentsProperty) &&
                                         argumentsProperty.ValueKind == JsonValueKind.String
                        ? argumentsProperty.GetString()
                        : string.Empty;

                    var parseResult = toolCallParser.AddChunk(index, argumentsChunk, id, name);
                    if (parseResult.Complete && parseResult.Repaired)
                    {
                        eventSink?.Invoke(new AssistantRuntimeEvent
                        {
                            Stage = "tool-call-repaired",
                            ToolName = name ?? string.Empty,
                            Status = "repaired",
                            Message = $"Recovered a streamed tool call payload for '{name ?? "tool"}'."
                        });
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed chunks and continue the stream.
        }
    }

    private static async IAsyncEnumerable<string> ReadServerSentEventPayloadsAsync(
        StreamReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var dataLines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                if (dataLines.Count > 0)
                {
                    yield return string.Join("\n", dataLines);
                }

                yield break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (dataLines.Count == 0)
                {
                    continue;
                }

                yield return string.Join("\n", dataLines);
                dataLines.Clear();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                dataLines.Add(line["data:".Length..].Trim());
            }
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
                    {
                        if (item.ValueKind != JsonValueKind.Object ||
                            !item.TryGetProperty("text", out var textProperty) ||
                            textProperty.ValueKind != JsonValueKind.String)
                        {
                            return null;
                        }

                        var isThought = item.TryGetProperty("thought", out var thoughtProperty) &&
                            thoughtProperty.ValueKind == JsonValueKind.True;
                        return isThought ? null : textProperty.GetString();
                    })
                    .Where(static item => !string.IsNullOrWhiteSpace(item))),
            _ => string.Empty
        };
    }

    private static string ReadThinkingDelta(JsonElement delta)
    {
        foreach (var propertyName in new[]
        {
            "reasoning_content",
            "reasoningContent",
            "reasoning",
            "thought",
            "thinking"
        })
        {
            if (!delta.TryGetProperty(propertyName, out var value))
            {
                continue;
            }

            var text = ReadTextValue(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        if (delta.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            var text = string.Join(
                string.Empty,
                content.EnumerateArray()
                    .Select(static item =>
                    {
                        if (item.ValueKind != JsonValueKind.Object ||
                            !item.TryGetProperty("text", out var textProperty) ||
                            textProperty.ValueKind != JsonValueKind.String)
                        {
                            return null;
                        }

                        var isThought = item.TryGetProperty("thought", out var thoughtProperty) &&
                            thoughtProperty.ValueKind == JsonValueKind.True;
                        var isThinkingType = item.TryGetProperty("type", out var typeProperty) &&
                            typeProperty.ValueKind == JsonValueKind.String &&
                            typeProperty.GetString() is "thinking" or "reasoning";
                        return isThought || isThinkingType ? textProperty.GetString() : null;
                    })
                    .Where(static item => !string.IsNullOrWhiteSpace(item)));
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return string.Empty;
    }

    private static string ReadTextValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(
                string.Empty,
                value.EnumerateArray()
                    .Select(static item =>
                        item.ValueKind == JsonValueKind.Object &&
                        item.TryGetProperty("text", out var textProperty) &&
                        textProperty.ValueKind == JsonValueKind.String
                            ? textProperty.GetString()
                            : item.ValueKind == JsonValueKind.String
                                ? item.GetString()
                                : null)
                    .Where(static item => !string.IsNullOrWhiteSpace(item))),
            _ => string.Empty
        };

    internal sealed record StreamingReadResult(
        AssistantTurnResponse? Response,
        bool ShouldRetryNonStreaming,
        string RetryReason)
    {
        /// <summary>
        /// Executes completed
        /// </summary>
        /// <param name="response">The response payload</param>
        /// <returns>The resulting streaming read result</returns>
        public static StreamingReadResult Completed(AssistantTurnResponse response) => new(response, false, string.Empty);

        /// <summary>
        /// Executes retry
        /// </summary>
        /// <param name="reason">The reason</param>
        /// <returns>The resulting streaming read result</returns>
        public static StreamingReadResult Retry(string reason) => new(null, true, reason);

        /// <summary>
        /// Executes empty
        /// </summary>
        /// <returns>The resulting streaming read result</returns>
        public static StreamingReadResult Empty() => new(null, false, string.Empty);
    }
}
