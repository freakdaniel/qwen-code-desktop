namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Channel Pairing Request
/// </summary>
public sealed class ChannelPairingRequest
{
    /// <summary>
    /// Gets or sets the sender id
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Gets or sets the sender name
    /// </summary>
    public required string SenderName { get; init; }

    /// <summary>
    /// Gets or sets the code
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the created at utc
    /// </summary>
    public required string CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the minutes ago
    /// </summary>
    public required int MinutesAgo { get; init; }
}
