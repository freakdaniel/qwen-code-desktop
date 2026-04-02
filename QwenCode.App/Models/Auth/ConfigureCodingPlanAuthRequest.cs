namespace QwenCode.App.Models;

public sealed class ConfigureCodingPlanAuthRequest
{
    public required string Scope { get; init; }

    public required string Region { get; init; }

    public required string ApiKey { get; init; }

    public string Model { get; init; } = string.Empty;
}
