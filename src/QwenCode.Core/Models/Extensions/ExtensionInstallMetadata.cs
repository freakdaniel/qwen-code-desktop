namespace QwenCode.App.Models;

/// <summary>
/// Represents the Extension Install Metadata
/// </summary>
public sealed class ExtensionInstallMetadata
{
    /// <summary>
    /// Gets or sets the source
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets or sets the type
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the ref
    /// </summary>
    public string? Ref { get; init; }

    /// <summary>
    /// Gets or sets the auto update
    /// </summary>
    public bool? AutoUpdate { get; init; }

    /// <summary>
    /// Gets or sets the allow pre release
    /// </summary>
    public bool? AllowPreRelease { get; init; }

    /// <summary>
    /// Gets or sets the registry url
    /// </summary>
    public string? RegistryUrl { get; init; }

    /// <summary>
    /// Gets or sets the release tag
    /// </summary>
    public string? ReleaseTag { get; init; }
}
