using QwenCode.App.Runtime;

namespace QwenCode.App.Models;

/// <summary>
/// Represents the Arena Agent Status File
/// </summary>
public sealed class ArenaAgentStatusFile
{
    /// <summary>
    /// Gets or sets the agent id
    /// </summary>
    public string AgentId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the worktree name
    /// </summary>
    public string WorktreeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the worktree path
    /// </summary>
    public string WorktreePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the branch
    /// </summary>
    public string Branch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stop reason
    /// </summary>
    public string StopReason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stats
    /// </summary>
    public AssistantExecutionStats Stats { get; init; } = new();

    /// <summary>
    /// Gets or sets the current activity
    /// </summary>
    public string CurrentActivity { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the final summary
    /// </summary>
    public string FinalSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the error
    /// </summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the updated at utc
    /// </summary>
    public DateTime UpdatedAtUtc { get; init; }
}
