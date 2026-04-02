namespace QwenCode.App.Runtime;

public sealed class AssistantTurnResponse
{
    public required string Summary { get; init; }

    public required string ProviderName { get; init; }

    public string Model { get; init; } = string.Empty;

    public IReadOnlyList<AssistantToolCall> ToolCalls { get; init; } = [];

    public IReadOnlyList<AssistantToolCallResult> ToolExecutions { get; init; } = [];
}
