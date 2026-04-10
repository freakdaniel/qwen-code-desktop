namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Active Arena Session State
/// </summary>
public sealed class ActiveArenaSessionState
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the task
    /// </summary>
    public string Task { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the linked orchestration task id
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base branch
    /// </summary>
    public string BaseBranch { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the round count
    /// </summary>
    public int RoundCount { get; set; }

    /// <summary>
    /// Gets or sets the selected winner
    /// </summary>
    public string SelectedWinner { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the started at utc
    /// </summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the last updated at utc
    /// </summary>
    public DateTime LastUpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the stats
    /// </summary>
    public ArenaSessionStats Stats { get; set; } = new();

    /// <summary>
    /// Gets or sets the agents
    /// </summary>
    public IReadOnlyList<ArenaAgentStatusFile> Agents { get; set; } = [];
}
