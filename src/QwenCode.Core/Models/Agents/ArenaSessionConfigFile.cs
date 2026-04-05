namespace QwenCode.App.Models;

/// <summary>
/// Represents the Arena Session Config File
/// </summary>
public sealed class ArenaSessionConfigFile
{
    /// <summary>
    /// Gets or sets the arena session id
    /// </summary>
    public string ArenaSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the source repo path
    /// </summary>
    public string SourceRepoPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the task
    /// </summary>
    public string Task { get; init; } = string.Empty;

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
    /// Gets or sets the models
    /// </summary>
    public IReadOnlyList<ArenaModelDescriptor> Models { get; init; } = [];

    /// <summary>
    /// Gets or sets the worktree names
    /// </summary>
    public IReadOnlyList<string> WorktreeNames { get; init; } = [];

    /// <summary>
    /// Gets or sets the base branch
    /// </summary>
    public string BaseBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the created at utc
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the updated at utc
    /// </summary>
    public DateTime UpdatedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the agents
    /// </summary>
    public IReadOnlyDictionary<string, ArenaAgentStatusFile> Agents { get; init; } =
        new Dictionary<string, ArenaAgentStatusFile>(StringComparer.OrdinalIgnoreCase);
}
