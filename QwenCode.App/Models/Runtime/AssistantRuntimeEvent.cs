namespace QwenCode.App.Runtime;

public sealed class AssistantRuntimeEvent
{
    public required string Stage { get; init; }

    public required string Message { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string ContentDelta { get; init; } = string.Empty;

    public string ContentSnapshot { get; init; } = string.Empty;
}
