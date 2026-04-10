namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Channel Plugin Definition
/// </summary>
public sealed class ChannelPluginDefinition
{
    /// <summary>
    /// Gets or sets the extension name
    /// </summary>
    public required string ExtensionName { get; init; }

    /// <summary>
    /// Gets or sets the channel type
    /// </summary>
    public required string ChannelType { get; init; }

    /// <summary>
    /// Gets or sets the display name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the entry path
    /// </summary>
    public required string EntryPath { get; init; }

    /// <summary>
    /// Gets or sets the extension path
    /// </summary>
    public required string ExtensionPath { get; init; }
}
