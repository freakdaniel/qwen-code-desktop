namespace QwenCode.Core.Models;

/// <summary>
/// Represents an MCP resource definition discovered from a server.
/// </summary>
public sealed class McpResourceDefinition
{
    /// <summary>
    /// Gets or sets the server name.
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets or sets the resource uri.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets or sets the resource display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the mime type.
    /// </summary>
    public string MimeType { get; init; } = string.Empty;
}
