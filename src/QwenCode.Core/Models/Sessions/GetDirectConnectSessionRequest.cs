namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to fetch one direct-connect session.
/// </summary>
public sealed class GetDirectConnectSessionRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;
}
