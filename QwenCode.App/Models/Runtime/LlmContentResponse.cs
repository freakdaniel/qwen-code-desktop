namespace QwenCode.App.Runtime;

public sealed class LlmContentResponse
{
    public required string ProviderName { get; init; }

    public string Model { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string StopReason { get; init; } = string.Empty;
}
