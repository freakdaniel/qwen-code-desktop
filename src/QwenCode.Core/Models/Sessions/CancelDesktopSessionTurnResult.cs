namespace QwenCode.App.Models;

/// <summary>
/// Represents the Cancel Desktop Session Turn Result
/// </summary>
public sealed class CancelDesktopSessionTurnResult
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether cancelled
    /// </summary>
    public required bool Cancelled { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public required DateTime TimestampUtc { get; init; }
}
