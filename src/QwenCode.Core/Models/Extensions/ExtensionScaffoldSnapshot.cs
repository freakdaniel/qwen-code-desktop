namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Extension Scaffold Snapshot
/// </summary>
public sealed class ExtensionScaffoldSnapshot
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the template
    /// </summary>
    public required string Template { get; init; }

    /// <summary>
    /// Gets or sets the created manifest
    /// </summary>
    public required bool CreatedManifest { get; init; }

    /// <summary>
    /// Gets or sets the created files
    /// </summary>
    public required IReadOnlyList<string> CreatedFiles { get; init; }
}
