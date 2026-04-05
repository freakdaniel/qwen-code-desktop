namespace QwenCode.App.Models;

/// <summary>
/// Represents the Invoke Prompt Registry Entry Request
/// </summary>
public sealed class InvokePromptRegistryEntryRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments json
    /// </summary>
    public string ArgumentsJson { get; init; } = "{}";
}
