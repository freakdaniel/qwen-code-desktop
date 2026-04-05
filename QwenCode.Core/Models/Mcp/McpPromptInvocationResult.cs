namespace QwenCode.App.Models;

public sealed class McpPromptInvocationResult
{
    public required string ServerName { get; init; }

    public required string PromptName { get; init; }

    public string Output { get; init; } = string.Empty;
}
