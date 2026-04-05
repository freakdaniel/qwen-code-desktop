namespace QwenCode.App.Runtime;

public sealed class AssistantToolCall
{
    public required string Id { get; init; }

    public required string ToolName { get; init; }

    public string ArgumentsJson { get; init; } = "{}";
}
