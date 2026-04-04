using Microsoft.Extensions.Options;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

public sealed class WorkspaceProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IWorkspaceInspectionService workspaceInspectionService,
    IGitWorktreeService gitWorktreeService,
    IGitHistoryService gitHistoryService) : IDesktopWorkspaceProjectionService
{
    private readonly DesktopShellOptions _options = options.Value;

    public Task<WorkspaceSnapshot> GetSnapshotAsync()
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }

    public Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        gitHistoryService.CreateCheckpoint(workspace, request);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }

    public Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        gitHistoryService.RestoreCheckpoint(workspace, request);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }

    public Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        gitWorktreeService.CreateManagedWorktree(workspace, request);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }

    public Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        gitWorktreeService.CleanupManagedSession(workspace, request);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }
}
