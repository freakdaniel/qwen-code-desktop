using QwenCode.App.Models;

namespace QwenCode.App.Config;

/// <summary>
/// Defines the contract for Config Service
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting runtime config snapshot</returns>
    RuntimeConfigSnapshot Inspect(WorkspacePaths paths);

    /// <summary>
    /// Resolves settings path
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="scope">The scope</param>
    /// <returns>The resulting string</returns>
    string ResolveSettingsPath(WorkspacePaths paths, string? scope);
}
