using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface IToolRegistry
{
    ToolCatalogSnapshot Inspect(WorkspacePaths paths);
}
