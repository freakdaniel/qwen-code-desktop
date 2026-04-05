using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Workspace Projection Service
/// </summary>
public interface IDesktopWorkspaceProjectionService
{
    /// <summary>
    /// Gets snapshot async
    /// </summary>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Creates git checkpoint async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request);

    /// <summary>
    /// Restores git checkpoint async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request);

    /// <summary>
    /// Creates managed worktree async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request);

    /// <summary>
    /// Cleans up managed session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request);
}
