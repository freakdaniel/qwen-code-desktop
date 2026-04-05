namespace QwenCode.App.Models;

public sealed class CancelDesktopSessionTurnResult
{
    public required string SessionId { get; init; }

    public required bool Cancelled { get; init; }

    public required string Message { get; init; }

    public required DateTime TimestampUtc { get; init; }
}
