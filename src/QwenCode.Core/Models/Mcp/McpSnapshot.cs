namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Mcp Snapshot
/// </summary>
public sealed class McpSnapshot
{
    /// <summary>
    /// Gets or sets the total count
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// Gets or sets the connected count
    /// </summary>
    public required int ConnectedCount { get; init; }

    /// <summary>
    /// Gets or sets the disconnected count
    /// </summary>
    public required int DisconnectedCount { get; init; }

    /// <summary>
    /// Gets or sets the missing count
    /// </summary>
    public required int MissingCount { get; init; }

    /// <summary>
    /// Gets or sets the token count
    /// </summary>
    public required int TokenCount { get; init; }

    /// <summary>
    /// Gets or sets the servers
    /// </summary>
    public required IReadOnlyList<McpServerDefinition> Servers { get; init; }
}
