using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class EmbeddingGenerationRequest
{
    public required string SessionId { get; init; }

    public required string Input { get; init; }

    public required string WorkingDirectory { get; init; }

    public required QwenRuntimeProfile RuntimeProfile { get; init; }

    public string ModelOverride { get; init; } = string.Empty;

    public string AuthTypeOverride { get; init; } = string.Empty;

    public string EndpointOverride { get; init; } = string.Empty;

    public string ApiKeyOverride { get; init; } = string.Empty;
}
