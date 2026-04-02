namespace QwenCode.App.Models;

public sealed class DismissInterruptedTurnResult
{
    public required string SessionId { get; init; }

    public required bool Dismissed { get; init; }

    public required string Message { get; init; }

    public required DateTime TimestampUtc { get; init; }
}
