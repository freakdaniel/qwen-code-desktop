namespace QwenCode.App.Models;

public sealed class RuntimeModelProviderSnapshot
{
    public string AuthType { get; init; } = string.Empty;

    public string Id { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string EnvironmentVariableName { get; init; } = string.Empty;
}
