using QwenCode.App.Models;

namespace QwenCode.App.Compatibility;

public interface ISettingsResolver
{
    QwenCompatibilitySnapshot InspectCompatibility(WorkspacePaths paths);

    QwenRuntimeProfile InspectRuntimeProfile(WorkspacePaths paths);
}
