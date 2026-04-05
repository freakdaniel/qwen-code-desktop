namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Llm Content Response
/// </summary>
public sealed class LlmContentResponse
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
    /// Gets or sets the content
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stop reason
    /// </summary>
    public string StopReason { get; init; } = string.Empty;
}
