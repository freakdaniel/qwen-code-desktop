namespace QwenCode.App.Models;

public sealed class ActiveArenaSessionState
{
    public string SessionId { get; set; } = string.Empty;

    public string Task { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string BaseBranch { get; set; } = string.Empty;

    public int RoundCount { get; set; }

    public string SelectedWinner { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }

    public ArenaSessionStats Stats { get; set; } = new();

    public IReadOnlyList<ArenaAgentStatusFile> Agents { get; set; } = [];
}
