using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

/// <summary>
/// Represents the Workspace Inspection Service
/// </summary>
/// <param name="gitWorktreeService">The git worktree service</param>
/// <param name="fileDiscoveryService">The file discovery service</param>
public sealed class WorkspaceInspectionService(
    IGitWorktreeService gitWorktreeService,
    IFileDiscoveryService fileDiscoveryService) : IWorkspaceInspectionService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting workspace snapshot</returns>
    public WorkspaceSnapshot Inspect(WorkspacePaths paths) =>
        new()
        {
            Git = gitWorktreeService.Inspect(paths),
            Discovery = fileDiscoveryService.Inspect(paths)
        };
}
