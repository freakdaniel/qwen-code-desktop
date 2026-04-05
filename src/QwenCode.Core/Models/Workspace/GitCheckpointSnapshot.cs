namespace QwenCode.App.Models;

/// <summary>
/// Represents the Git Checkpoint Snapshot
/// </summary>
public sealed class GitCheckpointSnapshot
{
    /// <summary>
    /// Gets or sets the commit hash
    /// </summary>
    public required string CommitHash { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the created at
    /// </summary>
    public required string CreatedAt { get; init; }
}
