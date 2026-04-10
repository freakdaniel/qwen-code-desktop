using QwenCode.Core.Models;

namespace QwenCode.Core.Infrastructure;

/// <summary>
/// Defines the contract for File Discovery Service
/// </summary>
public interface IFileDiscoveryService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting file discovery snapshot</returns>
    FileDiscoverySnapshot Inspect(WorkspacePaths paths);
}
