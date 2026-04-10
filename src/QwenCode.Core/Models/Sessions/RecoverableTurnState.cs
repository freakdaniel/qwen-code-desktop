namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Recoverable Turn State
/// </summary>
public sealed class RecoverableTurnState
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
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public required string GitBranch { get; init; }

    /// <summary>
    /// Gets or sets the recovery reason
    /// </summary>
    public required string RecoveryReason { get; init; }

    /// <summary>
    /// Gets or sets the last updated at utc
    /// </summary>
    public required DateTime LastUpdatedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the content snapshot
    /// </summary>
    public string ContentSnapshot { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;
}
