using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface IModelConfigResolver
{
    AvailableModel Resolve(
        WorkspacePaths paths,
        string? modelId = null,
        string? authType = null,
        bool embedding = false);
}
