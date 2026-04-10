namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Get Prompt Registry Request
/// </summary>
public sealed class GetPromptRegistryRequest
{
    /// <summary>
    /// Gets or sets the server name
    /// </summary>
    public string ServerName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the force refresh
    /// </summary>
    public bool ForceRefresh { get; init; }
}
