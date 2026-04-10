namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Assistant Execution Stats
/// </summary>
public sealed class AssistantExecutionStats
{
    /// <summary>
    /// Gets or sets the round count
    /// </summary>
    public int RoundCount { get; init; }

    /// <summary>
    /// Gets or sets the tool call count
    /// </summary>
    public int ToolCallCount { get; init; }

    /// <summary>
    /// Gets or sets the successful tool call count
    /// </summary>
    public int SuccessfulToolCallCount { get; init; }

    /// <summary>
    /// Gets or sets the failed tool call count
    /// </summary>
    public int FailedToolCallCount { get; init; }

    /// <summary>
    /// Gets or sets the duration ms
    /// </summary>
    public long DurationMs { get; init; }
}
