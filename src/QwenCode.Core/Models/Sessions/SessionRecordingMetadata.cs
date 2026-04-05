namespace QwenCode.App.Models;

/// <summary>
/// Represents the Session Recording Metadata
/// </summary>
public sealed class SessionRecordingMetadata
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public required string TranscriptPath { get; init; }

    /// <summary>
    /// Gets or sets the metadata path
    /// </summary>
    public required string MetadataPath { get; init; }

    /// <summary>
    /// Gets or sets the title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public required string GitBranch { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the started at
    /// </summary>
    public required string StartedAt { get; init; }

    /// <summary>
    /// Gets or sets the last updated at
    /// </summary>
    public required string LastUpdatedAt { get; init; }

    /// <summary>
    /// Gets or sets the last completed uuid
    /// </summary>
    public required string LastCompletedUuid { get; init; }

    /// <summary>
    /// Gets or sets the message count
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// Gets or sets the entry count
    /// </summary>
    public int EntryCount { get; init; }
}
