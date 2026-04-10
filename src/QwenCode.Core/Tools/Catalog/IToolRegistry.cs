using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

/// <summary>
/// Defines the contract for Tool Registry
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting tool catalog snapshot</returns>
    ToolCatalogSnapshot Inspect(WorkspacePaths paths);
}
