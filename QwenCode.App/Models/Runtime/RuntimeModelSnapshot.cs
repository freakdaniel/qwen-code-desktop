namespace QwenCode.App.Runtime;

public sealed class RuntimeModelSnapshot
{
    public string DefaultModelId { get; init; } = string.Empty;

    public string EmbeddingModelId { get; init; } = string.Empty;

    public string SelectedAuthType { get; init; } = string.Empty;

    public required IReadOnlyList<AvailableModel> AvailableModels { get; init; }
}
