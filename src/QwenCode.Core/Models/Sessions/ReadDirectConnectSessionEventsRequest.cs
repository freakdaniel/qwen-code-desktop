namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to read buffered direct-connect events.
/// </summary>
public sealed class ReadDirectConnectSessionEventsRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the exclusive event sequence cursor.
    /// </summary>
    public long AfterSequence { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of events to return.
    /// </summary>
    public int MaxCount { get; init; } = 100;
}
