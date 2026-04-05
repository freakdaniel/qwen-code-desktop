namespace QwenCode.App.Models;

/// <summary>
/// Represents the Ide Connection Snapshot
/// </summary>
public sealed class IdeConnectionSnapshot
{
    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = "disconnected";

    /// <summary>
    /// Gets or sets the details
    /// </summary>
    public string Details { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the ide
    /// </summary>
    public IdeInfo? Ide { get; init; }

    /// <summary>
    /// Gets or sets the workspace path
    /// </summary>
    public string WorkspacePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the port
    /// </summary>
    public string Port { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the command
    /// </summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the auth token
    /// </summary>
    public string AuthToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the supports diff
    /// </summary>
    public bool SupportsDiff { get; init; }

    /// <summary>
    /// Gets or sets the available tools
    /// </summary>
    public IReadOnlyList<string> AvailableTools { get; init; } = [];

    /// <summary>
    /// Gets or sets the context
    /// </summary>
    public IdeContextSnapshot? Context { get; init; }
}
