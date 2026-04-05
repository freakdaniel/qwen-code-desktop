namespace QwenCode.App.Runtime;

public sealed class RuntimeModelCapabilities
{
    public bool SupportsToolCalls { get; init; }

    public bool SupportsJsonOutput { get; init; }

    public bool SupportsStreaming { get; init; }

    public bool SupportsReasoning { get; init; }

    public bool SupportsEmbeddings { get; init; }

    public int? ContextWindowTokens { get; init; }

    public int? MaxOutputTokens { get; init; }
}
