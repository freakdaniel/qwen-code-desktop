using QwenCode.App.Models;

namespace QwenCode.App.Compatibility;

public interface ISettingsResolver
{
    QwenCompatibilitySnapshot InspectCompatibility(SourceMirrorPaths paths);

    QwenRuntimeProfile InspectRuntimeProfile(SourceMirrorPaths paths);
}
