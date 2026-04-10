namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Cancel Desktop Session Turn Request
/// </summary>
public sealed class CancelDesktopSessionTurnRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}
