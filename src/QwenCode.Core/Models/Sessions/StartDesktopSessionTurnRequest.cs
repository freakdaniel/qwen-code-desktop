namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Start Desktop Session Turn Request
/// </summary>
public sealed class StartDesktopSessionTurnRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the prompt
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the desktop surface context.
    /// </summary>
    public string SurfaceContext { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool arguments json
    /// </summary>
    public string ToolArgumentsJson { get; init; } = "{}";

    /// <summary>
    /// Gets or sets the approve tool execution
    /// </summary>
    public bool ApproveToolExecution { get; init; }
}
