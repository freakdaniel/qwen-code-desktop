namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Subagent Tool Execution Record
/// </summary>
public sealed class SubagentToolExecutionRecord
{
    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approval state
    /// </summary>
    public string ApprovalState { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the changed files
    /// </summary>
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];
}
