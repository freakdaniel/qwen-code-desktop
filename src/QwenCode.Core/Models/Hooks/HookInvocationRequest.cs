using System.Text.Json.Nodes;

namespace QwenCode.App.Models;

/// <summary>
/// Represents the Hook Invocation Request
/// </summary>
public sealed class HookInvocationRequest
{
    /// <summary>
    /// Gets or sets the event name
    /// </summary>
    public required HookEventName EventName { get; init; }

    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public required string TranscriptPath { get; init; }

    /// <summary>
    /// Gets or sets the prompt
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool status
    /// </summary>
    public string ToolStatus { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approval state
    /// </summary>
    public string ApprovalState { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool arguments json
    /// </summary>
    public string ToolArgumentsJson { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool output
    /// </summary>
    public string ToolOutput { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool error message
    /// </summary>
    public string ToolErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the reason
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the metadata
    /// </summary>
    public JsonObject Metadata { get; init; } = [];
}
