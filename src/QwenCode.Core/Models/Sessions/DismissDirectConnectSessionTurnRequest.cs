namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to dismiss an interrupted turn through a direct-connect session.
/// </summary>
public sealed class DismissDirectConnectSessionTurnRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the wrapped dismiss request.
    /// </summary>
    public DismissInterruptedTurnRequest Turn { get; init; } = new()
    {
        SessionId = string.Empty
    };
}
