namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Set Extension Setting Value Request
/// </summary>
public sealed class SetExtensionSettingValueRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the setting
    /// </summary>
    public required string Setting { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the value
    /// </summary>
    public required string Value { get; init; }
}
