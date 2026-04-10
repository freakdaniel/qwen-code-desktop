namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Runtime Model Snapshot
/// </summary>
public sealed class RuntimeModelSnapshot
{
    /// <summary>
    /// Gets or sets the default model id
    /// </summary>
    public string DefaultModelId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the embedding model id
    /// </summary>
    public string EmbeddingModelId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected auth type
    /// </summary>
    public string SelectedAuthType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the available models
    /// </summary>
    public required IReadOnlyList<AvailableModel> AvailableModels { get; init; }
}
