namespace QwenCode.App.Models;

/// <summary>
/// Represents the Remove Extension Request
/// </summary>
public sealed class RemoveExtensionRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }
}
