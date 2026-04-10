namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Dismiss Interrupted Turn Result
/// </summary>
public sealed class DismissInterruptedTurnResult
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the dismissed
    /// </summary>
    public required bool Dismissed { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public required DateTime TimestampUtc { get; init; }
}
