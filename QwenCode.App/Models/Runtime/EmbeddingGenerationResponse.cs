namespace QwenCode.App.Runtime;

public sealed class EmbeddingGenerationResponse
{
    public required string ProviderName { get; init; }

    public string Model { get; init; } = string.Empty;

    public required IReadOnlyList<float> Embedding { get; init; }
}
