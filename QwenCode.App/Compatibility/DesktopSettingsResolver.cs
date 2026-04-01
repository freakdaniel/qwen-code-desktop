using QwenCode.App.Models;

namespace QwenCode.App.Compatibility;

public sealed class DesktopSettingsResolver(
    QwenCompatibilityService compatibilityService,
    QwenRuntimeProfileService runtimeProfileService) : ISettingsResolver
{
    public QwenCompatibilitySnapshot InspectCompatibility(SourceMirrorPaths paths) =>
        compatibilityService.Inspect(paths);

    public QwenRuntimeProfile InspectRuntimeProfile(SourceMirrorPaths paths) =>
        runtimeProfileService.Inspect(paths);
}
