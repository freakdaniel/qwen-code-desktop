using QwenCode.App.Models;

namespace QwenCode.App.Compatibility;

/// <summary>
/// Defines the contract for Settings Resolver
/// </summary>
public interface ISettingsResolver
{
    /// <summary>
    /// Executes inspect compatibility
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting qwen compatibility snapshot</returns>
    QwenCompatibilitySnapshot InspectCompatibility(WorkspacePaths paths);

    /// <summary>
    /// Executes inspect runtime profile
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting qwen runtime profile</returns>
    QwenRuntimeProfile InspectRuntimeProfile(WorkspacePaths paths);
}
