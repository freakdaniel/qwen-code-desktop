namespace QwenCode.App.Models;

public sealed class ArenaSessionStatusFile
{
    public string SessionId { get; init; } = string.Empty;

    public string Task { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string BaseBranch { get; init; } = string.Empty;

    public int RoundCount { get; init; }

    public string SelectedWinner { get; init; } = string.Empty;

    public string AppliedWinner { get; init; } = string.Empty;

    public DateTime StartedAtUtc { get; init; }

    public DateTime? EndedAtUtc { get; init; }

    public ArenaSessionStats Stats { get; init; } = new();

    public IReadOnlyList<ArenaAgentStatusFile> Agents { get; init; } = [];
}
