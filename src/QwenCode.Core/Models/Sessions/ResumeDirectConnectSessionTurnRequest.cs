namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to resume an interrupted turn through a direct-connect session.
/// </summary>
public sealed class ResumeDirectConnectSessionTurnRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the wrapped resume request.
    /// </summary>
    public ResumeInterruptedTurnRequest Turn { get; init; } = new()
    {
        SessionId = string.Empty
    };
}
