using QwenCode.Core.Models;

namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Session Preview
/// </summary>
public sealed class SessionPreview
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the title
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets or sets the last activity
    /// </summary>
    public required string LastActivity { get; init; }

    /// <summary>
    /// Gets or sets the started at
    /// </summary>
    public required string StartedAt { get; init; }

    /// <summary>
    /// Gets or sets the last updated at
    /// </summary>
    public required string LastUpdatedAt { get; init; }

    /// <summary>
    /// Gets or sets the category
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets or sets the mode
    /// </summary>
    public required DesktopMode Mode { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public required string GitBranch { get; init; }

    /// <summary>
    /// Gets or sets the message count
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public required string TranscriptPath { get; init; }

    /// <summary>
    /// Gets or sets the metadata path
    /// </summary>
    public required string MetadataPath { get; init; }
}
