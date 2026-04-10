namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Install Extension Request
/// </summary>
public sealed class InstallExtensionRequest
{
    /// <summary>
    /// Gets or sets the source path
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Gets or sets the install mode
    /// </summary>
    public required string InstallMode { get; init; }

    /// <summary>
    /// Gets or sets the source type
    /// </summary>
    public string SourceType { get; init; } = "local";

    /// <summary>
    /// Gets or sets the ref
    /// </summary>
    public string Ref { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the auto update
    /// </summary>
    public bool AutoUpdate { get; init; }

    /// <summary>
    /// Gets or sets the allow pre release
    /// </summary>
    public bool AllowPreRelease { get; init; }

    /// <summary>
    /// Gets or sets the registry url
    /// </summary>
    public string RegistryUrl { get; init; } = string.Empty;
}
