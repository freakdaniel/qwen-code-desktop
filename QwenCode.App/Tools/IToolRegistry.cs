using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface IToolRegistry
{
    QwenToolCatalogSnapshot Inspect(SourceMirrorPaths paths);
}
