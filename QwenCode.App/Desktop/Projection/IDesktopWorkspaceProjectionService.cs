using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopWorkspaceProjectionService
{
    Task<WorkspaceSnapshot> GetSnapshotAsync();

    Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request);

    Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request);

    Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request);

    Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request);
}
