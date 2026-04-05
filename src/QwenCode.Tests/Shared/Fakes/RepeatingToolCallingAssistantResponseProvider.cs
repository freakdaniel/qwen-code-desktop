namespace QwenCode.Tests.Shared.Fakes;

internal sealed class RepeatingToolCallingAssistantResponseProvider : IAssistantResponseProvider
{
    public string Name => "loop-provider";

    public Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<AssistantTurnResponse?>(
            new AssistantTurnResponse
            {
                Summary = string.Empty,
                ProviderName = Name,
                Model = "loop-provider-model",
                ToolCalls =
                [
                    new AssistantToolCall
                    {
                        Id = $"repeat-{toolHistory.Count + 1}",
                        ToolName = "read_file",
                        ArgumentsJson = """{"file_path":"README.md"}"""
                    }
                ]
            });
}
