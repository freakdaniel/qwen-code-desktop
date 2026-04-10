namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Extension Snapshot
/// </summary>
public sealed class ExtensionSnapshot
{
    /// <summary>
    /// Gets or sets the total count
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Gets or sets the active count
    /// </summary>
    public required int ActiveCount { get; init; }

    /// <summary>
    /// Gets or sets the linked count
    /// </summary>
    public required int LinkedCount { get; init; }

    /// <summary>
    /// Gets or sets the missing count
    /// </summary>
    public required int MissingCount { get; init; }

    /// <summary>
    /// Gets or sets the extensions
    /// </summary>
    public required IReadOnlyList<ExtensionDefinition> Extensions { get; init; }
}
