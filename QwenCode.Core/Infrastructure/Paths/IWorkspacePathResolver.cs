using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

public interface IWorkspacePathResolver
{
    WorkspacePaths Resolve(WorkspacePaths configured);
}
