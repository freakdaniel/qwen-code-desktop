namespace QwenCode.App.Models;

/// <summary>
/// Represents the Set Extension Enabled Request
/// </summary>
public sealed class SetExtensionEnabledRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the enabled
    /// </summary>
    public required bool Enabled { get; init; }
}
