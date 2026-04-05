namespace QwenCode.App.Models;

public sealed class GitHistorySnapshot
{
    public required bool IsInitialized { get; init; }

    public required string HistoryDirectory { get; init; }

    public required int CheckpointCount { get; init; }

    public required string CurrentCheckpoint { get; init; }

    public required IReadOnlyList<GitCheckpointSnapshot> RecentCheckpoints { get; init; }
}
