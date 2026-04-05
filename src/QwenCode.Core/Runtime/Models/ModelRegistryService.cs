using QwenCode.App.Config;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Model Registry Service
/// </summary>
/// <param name="configService">The config service</param>
public sealed class ModelRegistryService(IConfigService configService) : IModelRegistry
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting runtime model snapshot</returns>
    public RuntimeModelSnapshot Inspect(WorkspacePaths paths)
    {
        var snapshot = configService.Inspect(paths);
        var availableModels = BuildAvailableModels(snapshot);

        return new RuntimeModelSnapshot
        {
            DefaultModelId = snapshot.ModelName,
            EmbeddingModelId = snapshot.EmbeddingModel,
            SelectedAuthType = snapshot.SelectedAuthType,
            AvailableModels = availableModels
        };
    }

    private static IReadOnlyList<AvailableModel> BuildAvailableModels(RuntimeConfigSnapshot snapshot)
    {
        var models = new List<AvailableModel>();
        foreach (var provider in snapshot.ModelProviders)
        {
            models.Add(new AvailableModel
            {
                Id = provider.Id,
                AuthType = string.IsNullOrWhiteSpace(provider.AuthType) ? snapshot.SelectedAuthType : provider.AuthType,
                BaseUrl = provider.BaseUrl,
                ApiKeyEnvironmentVariable = provider.EnvironmentVariableName,
                Source = "model-provider",
                IsDefaultModel = string.Equals(provider.Id, snapshot.ModelName, StringComparison.OrdinalIgnoreCase),
                IsEmbeddingModel = string.Equals(provider.Id, snapshot.EmbeddingModel, StringComparison.OrdinalIgnoreCase),
                Capabilities = InferCapabilities(provider.Id, embedding: string.Equals(provider.Id, snapshot.EmbeddingModel, StringComparison.OrdinalIgnoreCase))
            });
        }

        EnsureSyntheticModel(models, snapshot.ModelName, snapshot.SelectedAuthType, isDefault: true, isEmbedding: false);
        EnsureSyntheticModel(models, snapshot.EmbeddingModel, snapshot.SelectedAuthType, isDefault: false, isEmbedding: true);

        return models
            .OrderByDescending(static item => item.IsDefaultModel)
            .ThenByDescending(static item => item.IsEmbeddingModel)
            .ThenBy(static item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void EnsureSyntheticModel(
        ICollection<AvailableModel> models,
        string modelId,
        string authType,
        bool isDefault,
        bool isEmbedding)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        var existing = models.FirstOrDefault(item =>
            string.Equals(item.Id, modelId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.AuthType, authType, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return;
        }

        models.Add(new AvailableModel
        {
            Id = modelId,
            AuthType = string.IsNullOrWhiteSpace(authType) ? "openai" : authType,
            BaseUrl = string.Empty,
            ApiKeyEnvironmentVariable = string.Empty,
            Source = isEmbedding ? "embedding-model" : "default-model",
            IsDefaultModel = isDefault,
            IsEmbeddingModel = isEmbedding,
            Capabilities = InferCapabilities(modelId, isEmbedding)
        });
    }

    private static RuntimeModelCapabilities InferCapabilities(string modelId, bool embedding)
    {
        if (embedding || modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimeModelCapabilities
            {
                SupportsEmbeddings = true,
                SupportsJsonOutput = false,
                SupportsStreaming = false,
                SupportsToolCalls = false,
                SupportsReasoning = false
            };
        }

        return new RuntimeModelCapabilities
        {
            SupportsEmbeddings = false,
            SupportsJsonOutput = true,
            SupportsStreaming = true,
            SupportsToolCalls = true,
            SupportsReasoning = modelId.Contains("qwen", StringComparison.OrdinalIgnoreCase) ||
                                modelId.Contains("reason", StringComparison.OrdinalIgnoreCase)
        };
    }
}
