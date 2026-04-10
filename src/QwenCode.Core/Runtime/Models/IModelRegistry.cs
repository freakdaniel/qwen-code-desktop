using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Defines the contract for Model Registry
/// </summary>
public interface IModelRegistry
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting runtime model snapshot</returns>
    RuntimeModelSnapshot Inspect(WorkspacePaths paths);
}
