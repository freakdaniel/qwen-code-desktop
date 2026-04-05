namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Json Generation Request
/// </summary>
public sealed class JsonGenerationRequest
{
    /// <summary>
    /// Gets or sets the content request
    /// </summary>
    public required LlmContentRequest ContentRequest { get; init; }
}
