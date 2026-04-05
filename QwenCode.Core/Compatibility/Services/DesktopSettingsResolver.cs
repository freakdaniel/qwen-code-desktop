using QwenCode.App.Models;

namespace QwenCode.App.Compatibility;

public sealed class DesktopSettingsResolver(
    QwenCompatibilityService compatibilityService,
    QwenRuntimeProfileService runtimeProfileService) : ISettingsResolver
{
    public QwenCompatibilitySnapshot InspectCompatibility(WorkspacePaths paths) =>
        compatibilityService.Inspect(paths);

    public QwenRuntimeProfile InspectRuntimeProfile(WorkspacePaths paths) =>
        runtimeProfileService.Inspect(paths);
}
