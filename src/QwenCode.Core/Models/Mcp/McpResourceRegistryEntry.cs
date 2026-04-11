namespace QwenCode.Core.Models;

/// <summary>
/// Represents a registered MCP resource entry.
/// </summary>
public sealed class McpResourceRegistryEntry
{
    /// <summary>
    /// Gets or sets the registry-safe name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the resource uri.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets or sets the qualified resource name.
    /// </summary>
    public required string QualifiedName { get; init; }

    /// <summary>
    /// Gets or sets the server name.
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the mime type.
    /// </summary>
    public string MimeType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public string Source { get; init; } = "mcp";
}
