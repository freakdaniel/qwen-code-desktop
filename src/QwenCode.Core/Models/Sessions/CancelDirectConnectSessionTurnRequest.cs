namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to cancel an active turn through a direct-connect session.
/// </summary>
public sealed class CancelDirectConnectSessionTurnRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the wrapped cancel request.
    /// </summary>
    public CancelDesktopSessionTurnRequest Turn { get; init; } = new();
}
