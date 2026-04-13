namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Interrupted Turn State
/// </summary>
public sealed class InterruptedTurnState
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the prompt
    /// </summary>
    public required string Prompt { get; init; }

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
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the interrupted at utc
    /// </summary>
    public required DateTime InterruptedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the last updated at utc
    /// </summary>
    public required DateTime LastUpdatedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the content snapshot
    /// </summary>
    public string ContentSnapshot { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the thinking snapshot
    /// </summary>
    public string ThinkingSnapshot { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;
}
