namespace QwenCode.App.Models;

/// <summary>
/// Represents the Extension Settings Snapshot
/// </summary>
public sealed class ExtensionSettingsSnapshot
{
    /// <summary>
    /// Gets or sets the extension name
    /// </summary>
    public required string ExtensionName { get; init; }

    /// <summary>
    /// Gets or sets the version
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets or sets the install type
    /// </summary>
    public required string InstallType { get; init; }

    /// <summary>
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the settings
    /// </summary>
    public required IReadOnlyList<ExtensionSettingValue> Settings { get; init; }
}
