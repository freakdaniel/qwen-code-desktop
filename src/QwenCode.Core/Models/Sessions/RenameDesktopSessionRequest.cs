namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Rename Desktop Session Request
/// </summary>
public sealed class RenameDesktopSessionRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the title
    /// </summary>
    public string Title { get; init; } = string.Empty;
}
