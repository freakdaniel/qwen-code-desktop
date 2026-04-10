using QwenCode.Core.Models;

namespace QwenCode.Core.Compatibility;

/// <summary>
/// Represents the Desktop Settings Resolver
/// </summary>
/// <param name="compatibilityService">The compatibility service</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
public sealed class DesktopSettingsResolver(
    QwenCompatibilityService compatibilityService,
    QwenRuntimeProfileService runtimeProfileService) : ISettingsResolver
{
    /// <summary>
    /// Executes inspect compatibility
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting qwen compatibility snapshot</returns>
    public QwenCompatibilitySnapshot InspectCompatibility(WorkspacePaths paths) =>
        compatibilityService.Inspect(paths);

    /// <summary>
    /// Executes inspect runtime profile
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting qwen runtime profile</returns>
    public QwenRuntimeProfile InspectRuntimeProfile(WorkspacePaths paths) =>
        runtimeProfileService.Inspect(paths);
}
