namespace QwenCode.App.Models;

/// <summary>
/// Represents the Git Repository Snapshot
/// </summary>
public sealed class GitRepositorySnapshot
{
    /// <summary>
    /// Gets or sets a value indicating whether is git available
    /// </summary>
    public required bool IsGitAvailable { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is repository
    /// </summary>
    public required bool IsRepository { get; init; }

    /// <summary>
    /// Gets or sets the worktree supported
    /// </summary>
    public required bool WorktreeSupported { get; init; }

    /// <summary>
    /// Gets or sets the repository root
    /// </summary>
    public string RepositoryRoot { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the current branch
    /// </summary>
    public string CurrentBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the current commit
    /// </summary>
    public string CurrentCommit { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the git version
    /// </summary>
    public string GitVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the managed session count
    /// </summary>
    public required int ManagedSessionCount { get; init; }

    /// <summary>
    /// Gets or sets the managed worktrees root
    /// </summary>
    public required string ManagedWorktreesRoot { get; init; }

    /// <summary>
    /// Gets or sets the worktrees
    /// </summary>
    public required IReadOnlyList<GitWorktreeEntry> Worktrees { get; init; }

    /// <summary>
    /// Gets or sets the history
    /// </summary>
    public required GitHistorySnapshot History { get; init; }
}
