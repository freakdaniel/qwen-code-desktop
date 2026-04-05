namespace QwenCode.App.Models;

/// <summary>
/// Represents the Runtime Settings Layer Snapshot
/// </summary>
public sealed class RuntimeSettingsLayerSnapshot
{
    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the source
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the included
    /// </summary>
    public required bool Included { get; init; }
}
