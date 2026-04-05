using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Model Config Resolver
/// </summary>
/// <param name="modelRegistry">The model registry</param>
public sealed class ModelConfigResolver(IModelRegistry modelRegistry) : IModelConfigResolver
{
    /// <summary>
    /// Resolves value
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="modelId">The model id</param>
    /// <param name="authType">The auth type</param>
    /// <param name="embedding">The embedding</param>
    /// <returns>The resulting available model</returns>
    public AvailableModel Resolve(
        WorkspacePaths paths,
        string? modelId = null,
        string? authType = null,
        bool embedding = false)
    {
        var snapshot = modelRegistry.Inspect(paths);
        var preferredModelId = embedding ? snapshot.EmbeddingModelId : snapshot.DefaultModelId;
        var resolvedModelId = string.IsNullOrWhiteSpace(modelId) ? preferredModelId : modelId;
        var resolvedAuthType = string.IsNullOrWhiteSpace(authType) ? snapshot.SelectedAuthType : authType;

        var exact = snapshot.AvailableModels.FirstOrDefault(item =>
            string.Equals(item.Id, resolvedModelId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.AuthType, resolvedAuthType, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var byModel = snapshot.AvailableModels.FirstOrDefault(item =>
            string.Equals(item.Id, resolvedModelId, StringComparison.OrdinalIgnoreCase) &&
            item.IsEmbeddingModel == embedding);
        if (byModel is not null)
        {
            return byModel;
        }

        return snapshot.AvailableModels.First(item => item.IsEmbeddingModel == embedding || string.Equals(item.Id, preferredModelId, StringComparison.OrdinalIgnoreCase));
    }
}
