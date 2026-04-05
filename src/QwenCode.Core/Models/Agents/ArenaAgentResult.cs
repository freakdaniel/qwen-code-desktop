using QwenCode.App.Runtime;

namespace QwenCode.App.Models;

/// <summary>
/// Represents the Arena Agent Result
/// </summary>
public sealed class ArenaAgentResult
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
    /// Gets or sets the provider name
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stop reason
    /// </summary>
    public string StopReason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stats
    /// </summary>
    public AssistantExecutionStats Stats { get; init; } = new();

    /// <summary>
    /// Gets or sets the worktree path
    /// </summary>
    public string WorktreePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the branch
    /// </summary>
    public string Branch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public string TranscriptPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the diff
    /// </summary>
    public string Diff { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the modified files
    /// </summary>
    public IReadOnlyList<string> ModifiedFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets the tool executions
    /// </summary>
    public IReadOnlyList<SubagentToolExecutionRecord> ToolExecutions { get; init; } = [];

    /// <summary>
    /// Gets or sets the started at utc
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the ended at utc
    /// </summary>
    public DateTime EndedAtUtc { get; init; }
}
