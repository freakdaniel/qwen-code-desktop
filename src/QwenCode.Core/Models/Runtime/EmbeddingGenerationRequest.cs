using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Embedding Generation Request
/// </summary>
public sealed class EmbeddingGenerationRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the input
    /// </summary>
    public required string Input { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the runtime profile
    /// </summary>
    public required QwenRuntimeProfile RuntimeProfile { get; init; }

    /// <summary>
    /// Gets or sets the model override
    /// </summary>
    public string ModelOverride { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the auth type override
    /// </summary>
    public string AuthTypeOverride { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the endpoint override
    /// </summary>
    public string EndpointOverride { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the api key override
    /// </summary>
    public string ApiKeyOverride { get; init; } = string.Empty;
}
