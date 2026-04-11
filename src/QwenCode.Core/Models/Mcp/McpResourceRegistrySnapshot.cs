namespace QwenCode.Core.Models;

/// <summary>
/// Represents an MCP resource registry snapshot.
/// </summary>
public sealed class McpResourceRegistrySnapshot
{
    /// <summary>
    /// Gets or sets the total count.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets or sets the server count.
    /// </summary>
    public int ServerCount { get; init; }

    /// <summary>
    /// Gets or sets the resources.
    /// </summary>
    public IReadOnlyList<McpResourceRegistryEntry> Resources { get; init; } = [];
}
