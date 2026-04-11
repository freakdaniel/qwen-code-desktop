namespace QwenCode.Core.Models;

/// <summary>
/// Represents the request used to approve a pending tool through a direct-connect session.
/// </summary>
public sealed class ApproveDirectConnectSessionToolRequest
{
    /// <summary>
    /// Gets or sets the direct-connect session id.
    /// </summary>
    public string DirectConnectSessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the wrapped approval request.
    /// </summary>
    public ApproveDesktopSessionToolRequest Approval { get; init; } = new();
}
