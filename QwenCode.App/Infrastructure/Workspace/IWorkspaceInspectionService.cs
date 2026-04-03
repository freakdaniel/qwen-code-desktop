using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

public interface IWorkspaceInspectionService
{
    WorkspaceSnapshot Inspect(WorkspacePaths paths);
}
