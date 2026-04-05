using QwenCode.App.Models;

namespace QwenCode.App.Config;

public interface IConfigService
{
    RuntimeConfigSnapshot Inspect(WorkspacePaths paths);

    string ResolveSettingsPath(WorkspacePaths paths, string? scope);
}
