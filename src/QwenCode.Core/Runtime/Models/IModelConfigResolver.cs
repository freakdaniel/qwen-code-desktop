using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Defines the contract for Model Config Resolver
/// </summary>
public interface IModelConfigResolver
{
    /// <summary>
    /// Resolves value
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="modelId">The model id</param>
    /// <param name="authType">The auth type</param>
    /// <param name="embedding">The embedding</param>
    /// <returns>The resulting available model</returns>
    AvailableModel Resolve(
        WorkspacePaths paths,
        string? modelId = null,
        string? authType = null,
        bool embedding = false);
}
