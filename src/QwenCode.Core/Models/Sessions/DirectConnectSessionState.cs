namespace QwenCode.Core.Models;

/// <summary>
/// Represents the current state of a direct-connect session.
/// </summary>
public sealed class DirectConnectSessionState
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public required string DirectConnectSessionId { get; init; }

    /// <summary>
    /// Gets or sets the bound desktop session id.
    /// </summary>
    public string BoundSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the default working directory.
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the lifecycle status.
    /// </summary>
    public string Status { get; init; } = "active";

    /// <summary>
    /// Gets or sets when the direct-connect session was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets when the direct-connect session last observed activity.
    /// </summary>
    public DateTime LastActivityAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the latest buffered event sequence for this connection.
    /// </summary>
    public long LatestEventSequence { get; init; }
}
