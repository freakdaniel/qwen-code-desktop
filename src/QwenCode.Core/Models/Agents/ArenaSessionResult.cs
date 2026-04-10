namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Arena Session Result
/// </summary>
public sealed class ArenaSessionResult
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
    /// Gets or sets the linked orchestration task id
    /// </summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the base branch
    /// </summary>
    public string BaseBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact path
    /// </summary>
    public string ArtifactPath { get; init; } = string.Empty;

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
    /// Gets or sets the cleanup requested
    /// </summary>
    public bool CleanupRequested { get; init; }

    /// <summary>
    /// Gets or sets the started at utc
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the ended at utc
    /// </summary>
    public DateTime EndedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the stats
    /// </summary>
    public ArenaSessionStats Stats { get; init; } = new();

    /// <summary>
    /// Gets or sets the models
    /// </summary>
    public IReadOnlyList<ArenaModelDescriptor> Models { get; init; } = [];

    /// <summary>
    /// Gets or sets the agents
    /// </summary>
    public IReadOnlyList<ArenaAgentResult> Agents { get; init; } = [];
}
