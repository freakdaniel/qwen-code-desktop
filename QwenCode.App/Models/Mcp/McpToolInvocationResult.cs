namespace QwenCode.App.Models;

public sealed class McpToolInvocationResult
{
    public required string ServerName { get; init; }

    public required string ToolName { get; init; }

    public required string Output { get; init; }

    public bool IsError { get; init; }
}
