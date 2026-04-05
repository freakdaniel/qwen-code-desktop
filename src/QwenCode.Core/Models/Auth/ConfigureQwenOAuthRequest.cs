namespace QwenCode.App.Models;

/// <summary>
/// Represents the Configure Qwen O Auth Request
/// </summary>
public sealed class ConfigureQwenOAuthRequest
{
    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the access token
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// Gets or sets the refresh token
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the token type
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Gets or sets the resource url
    /// </summary>
    public string ResourceUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the id token
    /// </summary>
    public string IdToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the expires at utc
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
