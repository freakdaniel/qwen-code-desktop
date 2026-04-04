namespace QwenCode.App.Runtime;

public sealed class JsonGenerationRequest
{
    public required LlmContentRequest ContentRequest { get; init; }
}
