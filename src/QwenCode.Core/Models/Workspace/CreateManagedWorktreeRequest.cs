namespace QwenCode.App.Models;

/// <summary>
/// Represents the Create Managed Worktree Request
/// </summary>
public sealed class CreateManagedWorktreeRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the base branch
    /// </summary>
    public string BaseBranch { get; init; } = string.Empty;
}
