using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class AssistantToolCallResult
{
    public required AssistantToolCall ToolCall { get; init; }

    public required NativeToolExecutionResult Execution { get; init; }
}
