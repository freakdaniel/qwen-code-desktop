namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Session Recording Context
/// </summary>
public sealed class SessionRecordingContext
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public required string GitBranch { get; init; }

    /// <summary>
    /// Gets or sets the title hint
    /// </summary>
    public required string TitleHint { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }
}
