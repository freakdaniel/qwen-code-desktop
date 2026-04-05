namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Embedding Generation Response
/// </summary>
public sealed class EmbeddingGenerationResponse
{
    /// <summary>
    /// Gets or sets the provider name
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the embedding
    /// </summary>
    public required IReadOnlyList<float> Embedding { get; init; }
}
