namespace QwenCode.App.Models;

public sealed class McpToolDefinition
{
    public required string ServerName { get; init; }

    public required string Name { get; init; }

    public required string FullyQualifiedName { get; init; }

    public string Description { get; init; } = string.Empty;

    public string InputSchemaJson { get; init; } = "{}";

    public bool ReadOnlyHint { get; init; }

    public bool DestructiveHint { get; init; }

    public bool IdempotentHint { get; init; }

    public bool OpenWorldHint { get; init; }
}
