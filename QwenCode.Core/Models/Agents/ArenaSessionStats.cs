namespace QwenCode.App.Models;

public sealed class ArenaSessionStats
{
    public int AgentCount { get; init; }

    public int CompletedAgentCount { get; init; }

    public int FailedAgentCount { get; init; }

    public int RoundCount { get; init; }

    public int ToolCallCount { get; init; }

    public int SuccessfulToolCallCount { get; init; }

    public int FailedToolCallCount { get; init; }

    public long TotalDurationMs { get; init; }
}
