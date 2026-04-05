using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface IModelRegistry
{
    RuntimeModelSnapshot Inspect(WorkspacePaths paths);
}
