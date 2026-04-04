namespace QwenCode.App.Models;

public sealed class ArenaSessionEvent
{
    public string SessionId { get; init; } = string.Empty;

    public ArenaSessionEventKind Kind { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string AgentName { get; init; } = string.Empty;

    public int RoundCount { get; init; }

    public string SelectedWinner { get; init; } = string.Empty;

    public ArenaSessionStats Stats { get; init; } = new();

    public DateTime TimestampUtc { get; init; }
}
