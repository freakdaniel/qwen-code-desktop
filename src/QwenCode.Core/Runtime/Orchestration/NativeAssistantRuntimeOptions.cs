namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Native Assistant Runtime Options
/// </summary>
public sealed class NativeAssistantRuntimeOptions
{
    /// <summary>
    /// Represents the Section Name
    /// </summary>
    public const string SectionName = "NativeAssistantRuntime";

    /// <summary>
    /// Gets or sets the provider
    /// </summary>
    public string Provider { get; set; } = "qwen-compatible";

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the endpoint
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the api key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the api key environment variable
    /// </summary>
    public string ApiKeyEnvironmentVariable { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system prompt
    /// </summary>
    public string SystemPrompt { get; set; } = NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt;

    /// <summary>
    /// Gets or sets the temperature
    /// </summary>
    public double Temperature { get; set; } = 0.2d;

    /// <summary>
    /// Gets or sets the max tool iterations
    /// </summary>
    public int MaxToolIterations { get; set; } = 12;

    /// <summary>
    /// Gets or sets the input token limit
    /// </summary>
    public int? InputTokenLimit { get; set; }

    /// <summary>
    /// Gets or sets the output token limit
    /// </summary>
    public int? OutputTokenLimit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether follow-up suggestions are generated after completed turns
    /// </summary>
    public bool EnableSpeculativeFollowupSuggestions { get; set; }
}
