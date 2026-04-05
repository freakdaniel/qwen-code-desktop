namespace QwenCode.App.Models;

/// <summary>
/// Represents the Extension Setting Value
/// </summary>
public sealed class ExtensionSettingValue
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

    /// <summary>
    /// Gets or sets the user value
    /// </summary>
    public string UserValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the workspace value
    /// </summary>
    public string WorkspaceValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the effective value
    /// </summary>
    public string EffectiveValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether has user value
    /// </summary>
    public bool HasUserValue { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether has workspace value
    /// </summary>
    public bool HasWorkspaceValue { get; init; }
}
