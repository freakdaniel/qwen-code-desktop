using Microsoft.Extensions.Options;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Workspace Projection Service
/// </summary>
/// <param name="options">The options</param>
/// <param name="workspacePathResolver">The workspace path resolver</param>
/// <param name="workspaceInspectionService">The workspace inspection service</param>
/// <param name="gitWorktreeService">The git worktree service</param>
/// <param name="gitHistoryService">The git history service</param>
public sealed class WorkspaceProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IWorkspaceInspectionService workspaceInspectionService,
    IGitWorktreeService gitWorktreeService,
    IGitHistoryService gitHistoryService) : IDesktopWorkspaceProjectionService
{
    private readonly DesktopShellOptions _options = options.Value;

    /// <summary>
    /// Gets snapshot async
    /// </summary>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> GetSnapshotAsync()
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }

    /// <summary>
    /// Creates git checkpoint async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        gitHistoryService.CreateCheckpoint(workspace, request);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }

    /// <summary>
    /// Restores git checkpoint async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        gitHistoryService.RestoreCheckpoint(workspace, request);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }

    /// <summary>
    /// Creates managed worktree async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        gitWorktreeService.CreateManagedWorktree(workspace, request);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }

    /// <summary>
    /// Cleans up managed session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        gitWorktreeService.CleanupManagedSession(workspace, request);
        return Task.FromResult(workspaceInspectionService.Inspect(workspace));
    }
}
