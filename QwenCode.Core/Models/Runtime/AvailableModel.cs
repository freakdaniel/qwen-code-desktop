namespace QwenCode.App.Runtime;

public sealed class AvailableModel
{
    public required string Id { get; init; }

    public required string AuthType { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKeyEnvironmentVariable { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public bool IsDefaultModel { get; init; }

    public bool IsEmbeddingModel { get; init; }

    public required RuntimeModelCapabilities Capabilities { get; init; }
}
