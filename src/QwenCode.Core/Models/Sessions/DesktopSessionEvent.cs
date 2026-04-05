using QwenCode.App.Models;

namespace QwenCode.App.Models;

/// <summary>
/// Represents the Desktop Session Event
/// </summary>
public sealed class DesktopSessionEvent
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the kind
    /// </summary>
    public required DesktopSessionEventKind Kind { get; init; }

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public string GitBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the command name
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the content delta
    /// </summary>
    public string ContentDelta { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the content snapshot
    /// </summary>
    public string ContentSnapshot { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name
    /// </summary>
    public string AgentName { get; init; } = string.Empty;
}
