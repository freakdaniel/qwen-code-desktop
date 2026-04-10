namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Runtime Model Capabilities
/// </summary>
public sealed class RuntimeModelCapabilities
{
    /// <summary>
    /// Gets or sets the supports tool calls
    /// </summary>
    public bool SupportsToolCalls { get; init; }

    /// <summary>
    /// Gets or sets the supports json output
    /// </summary>
    public bool SupportsJsonOutput { get; init; }

    /// <summary>
    /// Gets or sets the supports streaming
    /// </summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>
    /// Gets or sets the supports reasoning
    /// </summary>
    public bool SupportsReasoning { get; init; }

    /// <summary>
    /// Gets or sets the supports embeddings
    /// </summary>
    public bool SupportsEmbeddings { get; init; }

    /// <summary>
    /// Gets or sets the context window tokens
    /// </summary>
    public int? ContextWindowTokens { get; init; }

    /// <summary>
    /// Gets or sets the max output tokens
    /// </summary>
    public int? MaxOutputTokens { get; init; }
}
