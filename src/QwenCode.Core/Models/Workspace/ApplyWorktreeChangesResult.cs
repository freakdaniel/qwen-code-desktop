namespace QwenCode.App.Models;

/// <summary>
/// Represents the Apply Worktree Changes Result
/// </summary>
public sealed class ApplyWorktreeChangesResult
{
    /// <summary>
    /// Gets or sets the source repository path
    /// </summary>
    public string SourceRepositoryPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the worktree path
    /// </summary>
    public string WorktreePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the applied files
    /// </summary>
    public IReadOnlyList<string> AppliedFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets the deleted files
    /// </summary>
    public IReadOnlyList<string> DeletedFiles { get; init; } = [];
}
