namespace QwenCode.App.Models;

/// <summary>
/// Represents the Git Worktree Entry
/// </summary>
public sealed class GitWorktreeEntry
{
    /// <summary>
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the branch
    /// </summary>
    public required string Branch { get; init; }

    /// <summary>
    /// Gets or sets the head
    /// </summary>
    public required string Head { get; init; }

    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether is current
    /// </summary>
    public required bool IsCurrent { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is managed
    /// </summary>
    public required bool IsManaged { get; init; }
}
