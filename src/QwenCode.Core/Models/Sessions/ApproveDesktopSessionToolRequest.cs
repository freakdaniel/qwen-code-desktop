namespace QwenCode.App.Models;

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
}
