namespace QwenCode.Core.Models;

/// <summary>
/// Represents the local direct-connect HTTP server state.
/// </summary>
public sealed class DirectConnectServerState
{
    /// <summary>
    /// Gets or sets whether the server is enabled by configuration.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets or sets whether the server is currently listening.
    /// </summary>
    public bool Listening { get; init; }

    /// <summary>
    /// Gets or sets the base URL for the local server.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the access token required by local HTTP clients.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the last startup error, if any.
    /// </summary>
    public string Error { get; init; } = string.Empty;
}
