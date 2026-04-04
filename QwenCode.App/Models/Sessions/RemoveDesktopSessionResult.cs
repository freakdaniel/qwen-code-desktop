namespace QwenCode.App.Models;

public sealed class RemoveDesktopSessionResult
{
    public required bool Removed { get; init; }

    public required string SessionId { get; init; }

    public required IReadOnlyList<SessionPreview> RecentSessions { get; init; }
}
