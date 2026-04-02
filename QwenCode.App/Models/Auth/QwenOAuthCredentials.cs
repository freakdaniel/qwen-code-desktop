namespace QwenCode.App.Models;

public sealed class QwenOAuthCredentials
{
    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public string IdToken { get; init; } = string.Empty;

    public string TokenType { get; init; } = "Bearer";

    public string ResourceUrl { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
