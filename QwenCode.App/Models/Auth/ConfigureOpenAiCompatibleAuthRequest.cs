namespace QwenCode.App.Models;

public sealed class ConfigureOpenAiCompatibleAuthRequest
{
    public required string Scope { get; init; }

    public string AuthType { get; init; } = "openai";

    public string Model { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string ApiKeyEnvironmentVariable { get; init; } = "OPENAI_API_KEY";
}
