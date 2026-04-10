namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Cleanup Managed Worktree Session Request
/// </summary>
public sealed class CleanupManagedWorktreeSessionRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }
}
