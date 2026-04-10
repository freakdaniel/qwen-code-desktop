using QwenCode.Core.Runtime;

namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Session Conversation Record
/// </summary>
public sealed class SessionConversationRecord
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
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public required string GitBranch { get; init; }

    /// <summary>
    /// Gets or sets the start time
    /// </summary>
    public required string StartTime { get; init; }

    /// <summary>
    /// Gets or sets the last updated
    /// </summary>
    public required string LastUpdated { get; init; }

    /// <summary>
    /// Gets or sets the model history
    /// </summary>
    public required IReadOnlyList<AssistantConversationMessage> ModelHistory { get; init; }
}
