namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Rename Desktop Session Result
/// </summary>
public sealed class RenameDesktopSessionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the session was renamed.
    /// </summary>
    public required bool Renamed { get; init; }

    /// <summary>
    /// Gets or sets the session id.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the recent sessions.
    /// </summary>
    public required IReadOnlyList<SessionPreview> RecentSessions { get; init; }
}
