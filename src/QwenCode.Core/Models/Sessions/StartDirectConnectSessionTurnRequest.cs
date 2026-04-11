namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to start a turn through a direct-connect session.
/// </summary>
public sealed class StartDirectConnectSessionTurnRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the wrapped turn request.
    /// </summary>
    public StartDesktopSessionTurnRequest Turn { get; init; } = new();
}
