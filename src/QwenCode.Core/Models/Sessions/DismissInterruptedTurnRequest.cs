namespace QwenCode.App.Models;

/// <summary>
/// Represents the Dismiss Interrupted Turn Request
/// </summary>
public sealed class DismissInterruptedTurnRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }
}
