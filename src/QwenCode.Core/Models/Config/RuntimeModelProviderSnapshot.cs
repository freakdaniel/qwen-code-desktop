namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Runtime Model Provider Snapshot
/// </summary>
public sealed class RuntimeModelProviderSnapshot
{
    /// <summary>
    /// Gets or sets the auth type
    /// </summary>
    public string AuthType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the id
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the base url
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment variable name
    /// </summary>
    public string EnvironmentVariableName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the explicit context window size override from generation config
    /// </summary>
    public int? ContextWindowSize { get; init; }

    /// <summary>
    /// Gets or sets the explicit output token limit override from generation config
    /// </summary>
    public int? MaxOutputTokens { get; init; }
}
