using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

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
