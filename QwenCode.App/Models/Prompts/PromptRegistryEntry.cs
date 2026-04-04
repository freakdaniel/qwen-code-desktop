namespace QwenCode.App.Models;

public sealed class PromptRegistryEntry
{
    public required string Name { get; init; }

    public required string PromptName { get; init; }

    public required string QualifiedName { get; init; }

    public required string ServerName { get; init; }

    public string Description { get; init; } = string.Empty;

    public string ArgumentsJson { get; init; } = "[]";

    public string Source { get; init; } = "mcp";
}
