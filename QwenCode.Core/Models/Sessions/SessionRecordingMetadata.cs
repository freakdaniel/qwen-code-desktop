namespace QwenCode.App.Models;

public sealed class SessionRecordingMetadata
{
    public required string SessionId { get; init; }

    public required string TranscriptPath { get; init; }

    public required string MetadataPath { get; init; }

    public required string Title { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string GitBranch { get; init; }

    public required string Status { get; init; }

    public required string StartedAt { get; init; }

    public required string LastUpdatedAt { get; init; }

    public required string LastCompletedUuid { get; init; }

    public int MessageCount { get; init; }

    public int EntryCount { get; init; }
}
