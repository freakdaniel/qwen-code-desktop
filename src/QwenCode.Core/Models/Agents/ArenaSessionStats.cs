namespace QwenCode.App.Models;

/// <summary>
/// Represents the Arena Session Stats
/// </summary>
public sealed class ArenaSessionStats
{
    /// <summary>
    /// Gets or sets the agent count
    /// </summary>
    public int AgentCount { get; init; }

    /// <summary>
    /// Gets or sets the completed agent count
    /// </summary>
    public int CompletedAgentCount { get; init; }

    /// <summary>
    /// Gets or sets the failed agent count
    /// </summary>
    public int FailedAgentCount { get; init; }

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
    /// Gets or sets the total duration ms
    /// </summary>
    public long TotalDurationMs { get; init; }
}
