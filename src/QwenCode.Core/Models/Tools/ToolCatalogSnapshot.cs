namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Tool Catalog Snapshot
/// </summary>
public sealed class ToolCatalogSnapshot
{
    /// <summary>
    /// Gets or sets the source mode
    /// </summary>
    public required string SourceMode { get; init; }

    /// <summary>
    /// Gets or sets the total count
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets or sets the allowed count
    /// </summary>
    public int AllowedCount { get; init; }

    /// <summary>
    /// Gets or sets the ask count
    /// </summary>
    public int AskCount { get; init; }

    /// <summary>
    /// Gets or sets the deny count
    /// </summary>
    public int DenyCount { get; init; }

    /// <summary>
    /// Gets or sets the tools
    /// </summary>
    public required IReadOnlyList<ToolDescriptor> Tools { get; init; }
}
