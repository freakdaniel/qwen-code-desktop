namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Mcp Resource Read Result
/// </summary>
public sealed class McpResourceReadResult
{
    /// <summary>
    /// Gets or sets the server name
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets or sets the uri
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets or sets the output
    /// </summary>
    public string Output { get; init; } = string.Empty;
}
