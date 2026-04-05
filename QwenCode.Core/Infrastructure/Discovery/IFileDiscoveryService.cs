using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

public interface IFileDiscoveryService
{
    FileDiscoverySnapshot Inspect(WorkspacePaths paths);
}
