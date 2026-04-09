using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Assistant Runtime Event
/// </summary>
public sealed class AssistantRuntimeEvent
{
    /// <summary>
    /// Gets or sets the stage
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the provider name
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool call id
    /// </summary>
    public string ToolCallId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool call group id
    /// </summary>
    public string ToolCallGroupId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool arguments json
    /// </summary>
    public string ToolArgumentsJson { get; init; } = "{}";

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

    /// <summary>
    /// Gets or sets the tool output body
    /// </summary>
    public string ToolOutput { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approval state
    /// </summary>
    public string ApprovalState { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the changed files
    /// </summary>
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets the questions
    /// </summary>
    public IReadOnlyList<DesktopQuestionPrompt> Questions { get; init; } = [];

    /// <summary>
    /// Gets or sets the answers
    /// </summary>
    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
