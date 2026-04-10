namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Active Turn State
/// </summary>
public sealed class ActiveTurnState
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the prompt
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public required string TranscriptPath { get; set; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public required string GitBranch { get; set; }

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stage
    /// </summary>
    public string Stage { get; set; } = "turn-started";

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; set; } = "started";

    /// <summary>
    /// Gets or sets the content snapshot
    /// </summary>
    public string ContentSnapshot { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the started at utc
    /// </summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the last updated at utc
    /// </summary>
    public DateTime LastUpdatedAtUtc { get; set; }
}
