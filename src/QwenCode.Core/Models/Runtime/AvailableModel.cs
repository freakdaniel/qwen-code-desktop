namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Available Model
/// </summary>
public sealed class AvailableModel
{
    /// <summary>
    /// Gets or sets the id
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the auth type
    /// </summary>
    public required string AuthType { get; init; }

    /// <summary>
    /// Gets or sets the base url
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the api key environment variable
    /// </summary>
    public string ApiKeyEnvironmentVariable { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the source
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the context window size
    /// </summary>
    public int ContextWindowSize { get; init; }

    /// <summary>
    /// Gets or sets the maximum output token count
    /// </summary>
    public int MaxOutputTokens { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is default model
    /// </summary>
    public bool IsDefaultModel { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is embedding model
    /// </summary>
    public bool IsEmbeddingModel { get; init; }

    /// <summary>
    /// Gets or sets the capabilities
    /// </summary>
    public required RuntimeModelCapabilities Capabilities { get; init; }
}
