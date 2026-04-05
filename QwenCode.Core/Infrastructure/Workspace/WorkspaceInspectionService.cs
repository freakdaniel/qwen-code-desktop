using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

public sealed class WorkspaceInspectionService(
    IGitWorktreeService gitWorktreeService,
    IFileDiscoveryService fileDiscoveryService) : IWorkspaceInspectionService
{
    public WorkspaceSnapshot Inspect(WorkspacePaths paths) =>
        new()
        {
            Git = gitWorktreeService.Inspect(paths),
            Discovery = fileDiscoveryService.Inspect(paths)
        };
}
