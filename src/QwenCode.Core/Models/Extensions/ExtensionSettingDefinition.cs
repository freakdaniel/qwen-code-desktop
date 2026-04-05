namespace QwenCode.App.Models;

/// <summary>
/// Represents the Extension Setting Definition
/// </summary>
public sealed class ExtensionSettingDefinition
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the environment variable
    /// </summary>
    public required string EnvironmentVariable { get; init; }

    /// <summary>
    /// Gets or sets the sensitive
    /// </summary>
    public required bool Sensitive { get; init; }
}
