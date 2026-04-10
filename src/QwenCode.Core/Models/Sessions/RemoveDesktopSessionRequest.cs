namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Remove Desktop Session Request
/// </summary>
public sealed class RemoveDesktopSessionRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}
