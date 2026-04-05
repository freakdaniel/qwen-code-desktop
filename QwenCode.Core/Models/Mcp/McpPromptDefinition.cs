namespace QwenCode.App.Models;

public sealed class McpPromptDefinition
{
    public required string ServerName { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    public string ArgumentsJson { get; init; } = "[]";
}
