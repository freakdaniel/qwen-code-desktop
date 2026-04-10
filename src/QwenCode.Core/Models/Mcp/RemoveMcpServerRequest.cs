namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Remove Mcp Server Request
/// </summary>
public sealed class RemoveMcpServerRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }
}
