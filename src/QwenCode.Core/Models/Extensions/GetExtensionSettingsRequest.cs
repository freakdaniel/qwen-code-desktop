namespace QwenCode.App.Models;

/// <summary>
/// Represents the Get Extension Settings Request
/// </summary>
public sealed class GetExtensionSettingsRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }
}
