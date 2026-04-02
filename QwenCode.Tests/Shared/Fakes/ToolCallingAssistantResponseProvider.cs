namespace QwenCode.Tests.Shared.Fakes;

internal sealed class ToolCallingAssistantResponseProvider(string targetFilePath) : IAssistantResponseProvider
{
    public string Name => "tool-provider";

    public Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        if (toolHistory.Count == 0)
        {
            return Task.FromResult<AssistantTurnResponse?>(
                new AssistantTurnResponse
                {
                    Summary = string.Empty,
                    ProviderName = Name,
                    Model = "tool-provider-model",
                    ToolCalls =
                    [
                        new AssistantToolCall
                        {
                            Id = "call-1",
                            ToolName = "read_file",
                            ArgumentsJson = $$"""{"file_path":"{{targetFilePath.Replace("\\", "\\\\")}}"}"""
                        }
                    ]
                });
        }

        return Task.FromResult<AssistantTurnResponse?>(
            new AssistantTurnResponse
            {
                Summary = $"Tool loop complete after {toolHistory.Count} native execution(s).",
                ProviderName = Name,
                Model = "tool-provider-model"
            });
    }
}
