namespace QwenCode.App.Runtime;

public sealed class AssistantExecutionStats
{
    public int RoundCount { get; init; }

    public int ToolCallCount { get; init; }

    public int SuccessfulToolCallCount { get; init; }

    public int FailedToolCallCount { get; init; }

    public long DurationMs { get; init; }
}
