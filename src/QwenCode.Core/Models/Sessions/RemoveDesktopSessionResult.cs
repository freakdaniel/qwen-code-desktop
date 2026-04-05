namespace QwenCode.App.Models;

/// <summary>
/// Represents the Remove Desktop Session Result
/// </summary>
public sealed class RemoveDesktopSessionResult
{
    /// <summary>
    /// Gets or sets the removed
    /// </summary>
    public required bool Removed { get; init; }

    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the recent sessions
    /// </summary>
    public required IReadOnlyList<SessionPreview> RecentSessions { get; init; }
}
