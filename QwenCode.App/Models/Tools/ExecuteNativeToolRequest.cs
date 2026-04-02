namespace QwenCode.App.Models;

public sealed class ExecuteNativeToolRequest
{
    public required string ToolName { get; init; }

    public string ArgumentsJson { get; init; } = "{}";

    public bool ApproveExecution { get; init; }
}
