namespace QwenCode.App.Options;

public sealed class NativeAssistantRuntimeOptions
{
    public const string SectionName = "NativeAssistantRuntime";

    public string Provider { get; set; } = "qwen-compatible";

    public string Model { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } =
        "You are the native Qwen Code Desktop runtime. Summarize the current turn clearly, " +
        "mention command or tool outcomes when relevant, and stay concise and actionable.";

    public double Temperature { get; set; } = 0.2d;

    public int MaxToolIterations { get; set; } = 4;

    public int? InputTokenLimit { get; set; }

    public int? OutputTokenLimit { get; set; }
}
