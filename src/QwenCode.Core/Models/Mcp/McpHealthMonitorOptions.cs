namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Mcp Health Monitor Options
/// </summary>
public sealed class McpHealthMonitorOptions
{
    /// <summary>
    /// Gets or sets the check interval
    /// </summary>
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the max consecutive failures
    /// </summary>
    public int MaxConsecutiveFailures { get; init; } = 3;

    /// <summary>
    /// Gets or sets the auto reconnect
    /// </summary>
    public bool AutoReconnect { get; init; } = true;

    /// <summary>
    /// Gets or sets the reconnect delay
    /// </summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);
}
