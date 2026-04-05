namespace QwenCode.App.Models;

/// <summary>
/// Represents the Git History Snapshot
/// </summary>
public sealed class GitHistorySnapshot
{
    /// <summary>
    /// Gets or sets a value indicating whether is initialized
    /// </summary>
    public required bool IsInitialized { get; init; }

    /// <summary>
    /// Gets or sets the history directory
    /// </summary>
    public required string HistoryDirectory { get; init; }

    /// <summary>
    /// Gets or sets the checkpoint count
    /// </summary>
    public required int CheckpointCount { get; init; }

    /// <summary>
    /// Gets or sets the current checkpoint
    /// </summary>
    public required string CurrentCheckpoint { get; init; }

    /// <summary>
    /// Gets or sets the recent checkpoints
    /// </summary>
    public required IReadOnlyList<GitCheckpointSnapshot> RecentCheckpoints { get; init; }
}
