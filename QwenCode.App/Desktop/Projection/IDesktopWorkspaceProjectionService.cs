using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopWorkspaceProjectionService
{
    Task<WorkspaceSnapshot> GetSnapshotAsync();

    Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request);

    Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request);
}
