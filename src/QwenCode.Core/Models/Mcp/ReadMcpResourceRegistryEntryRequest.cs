namespace QwenCode.Core.Models;

/// <summary>
/// Represents a request to read an MCP resource registry entry.
/// </summary>
public sealed class ReadMcpResourceRegistryEntryRequest
{
    /// <summary>
    /// Gets or sets the registry name, qualified name, or uri.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
