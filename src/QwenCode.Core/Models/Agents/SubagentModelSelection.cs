namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Subagent Model Selection
/// </summary>
public sealed class SubagentModelSelection
{
    /// <summary>
    /// Gets or sets the auth type
    /// </summary>
    public string AuthType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model id
    /// </summary>
    public string ModelId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the inherits
    /// </summary>
    public bool Inherits { get; init; }
}
