namespace QwenCode.App.Models;

public sealed class ConfigureQwenOAuthRequest
{
    public required string Scope { get; init; }

    public required string AccessToken { get; init; }

    public string RefreshToken { get; init; } = string.Empty;

    public string TokenType { get; init; } = "Bearer";

    public string ResourceUrl { get; init; } = string.Empty;

    public string IdToken { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
