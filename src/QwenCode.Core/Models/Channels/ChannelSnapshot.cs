namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Channel Snapshot
/// </summary>
public sealed class ChannelSnapshot
{
    /// <summary>
    /// Gets or sets a value indicating whether is service running
    /// </summary>
    public required bool IsServiceRunning { get; init; }

    /// <summary>
    /// Gets or sets the service process id
    /// </summary>
    public int? ServiceProcessId { get; init; }

    /// <summary>
    /// Gets or sets the service started at utc
    /// </summary>
    public string ServiceStartedAtUtc { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the service uptime text
    /// </summary>
    public string ServiceUptimeText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the supported types
    /// </summary>
    public required IReadOnlyList<string> SupportedTypes { get; init; }

    /// <summary>
    /// Gets or sets the channels
    /// </summary>
    public required IReadOnlyList<ChannelDefinition> Channels { get; init; }
}
