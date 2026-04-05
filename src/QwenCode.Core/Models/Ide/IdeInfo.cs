namespace QwenCode.App.Models;

/// <summary>
/// Represents the Ide Info
/// </summary>
public sealed class IdeInfo
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the display name
    /// </summary>
    public required string DisplayName { get; init; }
}
