namespace QwenCode.App.Models;

/// <summary>
/// Represents the Arena Session Status File
/// </summary>
public sealed class ArenaSessionStatusFile
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the task
    /// </summary>
    public string Task { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the base branch
    /// </summary>
    public string BaseBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the round count
    /// </summary>
    public int RoundCount { get; init; }

    /// <summary>
    /// Gets or sets the selected winner
    /// </summary>
    public string SelectedWinner { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the applied winner
    /// </summary>
    public string AppliedWinner { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the started at utc
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the ended at utc
    /// </summary>
    public DateTime? EndedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the stats
    /// </summary>
    public ArenaSessionStats Stats { get; init; } = new();

    /// <summary>
    /// Gets or sets the agents
    /// </summary>
    public IReadOnlyList<ArenaAgentStatusFile> Agents { get; init; } = [];
}
