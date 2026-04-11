namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Execute Native Tool Request
/// </summary>
public sealed class ExecuteNativeToolRequest
{
    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the arguments json
    /// </summary>
    public string ArgumentsJson { get; init; } = "{}";

    /// <summary>
    /// Gets or sets the session id for session-scoped permission rules.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approve execution
    /// </summary>
    public bool ApproveExecution { get; init; }
}
