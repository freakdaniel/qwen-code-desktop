namespace QwenCode.Core.Models;

/// <summary>
/// Represents a batch of buffered direct-connect session events.
/// </summary>
public sealed class DirectConnectSessionEventBatch
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public required string DirectConnectSessionId { get; init; }

    /// <summary>
    /// Gets or sets the latest known event sequence for this connection.
    /// </summary>
    public long LatestSequence { get; init; }

    /// <summary>
    /// Gets or sets the returned events.
    /// </summary>
    public IReadOnlyList<DirectConnectSessionEventRecord> Events { get; init; } = [];
}
