namespace QwenCode.App.Models;

public sealed class GitCheckpointSnapshot
{
    public required string CommitHash { get; init; }

    public required string Message { get; init; }

    public required string CreatedAt { get; init; }
}
