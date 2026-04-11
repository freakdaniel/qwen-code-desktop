namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to close a direct-connect session.
/// </summary>
public sealed class CloseDirectConnectSessionRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;
}
