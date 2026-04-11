namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Get MCP Resource Registry Request.
/// </summary>
public sealed class GetMcpResourceRegistryRequest
{
    /// <summary>
    /// Gets or sets the server name.
    /// </summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the force refresh.
    /// </summary>
    public bool ForceRefresh { get; init; }
}
