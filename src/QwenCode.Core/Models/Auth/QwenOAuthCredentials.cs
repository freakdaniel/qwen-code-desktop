namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Qwen O Auth Credentials
/// </summary>
public sealed class QwenOAuthCredentials
{
    /// <summary>
    /// Gets or sets the access token
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the refresh token
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the id token
    /// </summary>
    public string IdToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the token type
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Gets or sets the resource url
    /// </summary>
    public string ResourceUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the expires at utc
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
