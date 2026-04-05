using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Assistant Tool Call Result
/// </summary>
public sealed class AssistantToolCallResult
{
    /// <summary>
    /// Gets or sets the tool call
    /// </summary>
    public required AssistantToolCall ToolCall { get; init; }

    /// <summary>
    /// Gets or sets the execution
    /// </summary>
    public required NativeToolExecutionResult Execution { get; init; }
}
