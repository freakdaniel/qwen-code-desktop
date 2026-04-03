using Microsoft.Extensions.Options;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

public sealed class WorkspaceProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IWorkspaceInspectionService workspaceInspectionService,
    IGitWorktreeService gitWorktreeService) : IDesktopWorkspaceProjectionService
{
    private readonly DesktopShellOptions _options = options.Value;

    public Task<WorkspaceSnapshot> GetSnapshotAsync()
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
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
