namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Channel Pairing Snapshot
/// </summary>
public sealed class ChannelPairingSnapshot
{
    /// <summary>
    /// Gets or sets the channel name
    /// </summary>
    public required string ChannelName { get; init; }

    /// <summary>
    /// Gets or sets the pending count
    /// </summary>
    public required int PendingCount { get; init; }

    /// <summary>
    /// Gets or sets the allowlist count
    /// </summary>
    public required int AllowlistCount { get; init; }

    /// <summary>
    /// Gets or sets the pending requests
    /// </summary>
    public required IReadOnlyList<ChannelPairingRequest> PendingRequests { get; init; }
}
