namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Approve Desktop Session Tool Request
/// </summary>
public sealed class ApproveDesktopSessionToolRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the entry id
    /// </summary>
    public string EntryId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approval resolution decision.
    /// Supported values: allow-once, always-allow, always-allow-project, always-allow-user, always-allow-session, deny.
    /// </summary>
    public string Decision { get; init; } = "allow-once";

    /// <summary>
    /// Gets or sets the optional feedback that should be sent back to the assistant
    /// when the pending approval is denied.
    /// </summary>
    public string Feedback { get; init; } = string.Empty;
}
