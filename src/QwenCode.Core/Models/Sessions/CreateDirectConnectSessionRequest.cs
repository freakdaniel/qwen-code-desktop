namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to create a direct-connect session.
/// </summary>
public sealed class CreateDirectConnectSessionRequest
{
    /// <summary>
    /// Gets or sets the preferred desktop session id that should be reused.
    /// </summary>
    public string PreferredSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the default working directory for turns started through this connection.
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;
}
