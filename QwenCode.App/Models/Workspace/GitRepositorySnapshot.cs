namespace QwenCode.App.Models;

public sealed class GitRepositorySnapshot
{
    public required bool IsGitAvailable { get; init; }

    public required bool IsRepository { get; init; }

    public required bool WorktreeSupported { get; init; }

    public string RepositoryRoot { get; init; } = string.Empty;

    public string CurrentBranch { get; init; } = string.Empty;

    public string CurrentCommit { get; init; } = string.Empty;

    public string GitVersion { get; init; } = string.Empty;

    public required int ManagedSessionCount { get; init; }

    public required string ManagedWorktreesRoot { get; init; }

    public required IReadOnlyList<GitWorktreeEntry> Worktrees { get; init; }
}
