namespace QwenCode.App.Models;

/// <summary>
/// Represents the Mcp Reconnect Result
/// </summary>
public sealed class McpReconnectResult
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the attempted at utc
    /// </summary>
    public required DateTimeOffset AttemptedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the discovered tools count
    /// </summary>
    public int DiscoveredToolsCount { get; init; }

    /// <summary>
    /// Gets or sets the discovered prompts count
    /// </summary>
    public int DiscoveredPromptsCount { get; init; }

    /// <summary>
    /// Gets or sets the supports prompts
    /// </summary>
    public bool SupportsPrompts { get; init; }

    /// <summary>
    /// Gets or sets the supports resources
    /// </summary>
    public bool SupportsResources { get; init; }

    /// <summary>
    /// Gets or sets the last discovery utc
    /// </summary>
    public DateTimeOffset? LastDiscoveryUtc { get; init; }
}
