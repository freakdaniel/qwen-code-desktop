namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Mcp Server Definition
/// </summary>
public sealed class McpServerDefinition
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the transport
    /// </summary>
    public required string Transport { get; init; }

    /// <summary>
    /// Gets or sets the command or url
    /// </summary>
    public string CommandOrUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Gets or sets the environment variables
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the headers
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the timeout ms
    /// </summary>
    public int? TimeoutMs { get; init; }

    /// <summary>
    /// Gets or sets the trust
    /// </summary>
    public bool Trust { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the server-provided instructions
    /// </summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the include tools
    /// </summary>
    public IReadOnlyList<string> IncludeTools { get; init; } = [];

    /// <summary>
    /// Gets or sets the exclude tools
    /// </summary>
    public IReadOnlyList<string> ExcludeTools { get; init; } = [];

    /// <summary>
    /// Gets or sets the settings path
    /// </summary>
    public string SettingsPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = "unknown";

    /// <summary>
    /// Gets or sets the last reconnect attempt utc
    /// </summary>
    public DateTimeOffset? LastReconnectAttemptUtc { get; init; }

    /// <summary>
    /// Gets or sets the last error
    /// </summary>
    public string LastError { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether has persisted token
    /// </summary>
    public bool HasPersistedToken { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether a static authorization header is configured
    /// </summary>
    public bool HasStaticAuthorizationHeader { get; init; }

    /// <summary>
    /// Gets or sets the authentication status
    /// </summary>
    public string AuthenticationStatus { get; init; } = "none";

    /// <summary>
    /// Gets or sets the discovered tools count
    /// </summary>
    public int DiscoveredToolsCount { get; init; }

    /// <summary>
    /// Gets or sets the discovered prompts count
    /// </summary>
    public int DiscoveredPromptsCount { get; init; }

    /// <summary>
    /// Gets or sets the discovered resources count
    /// </summary>
    public int DiscoveredResourcesCount { get; init; }

    /// <summary>
    /// Gets or sets the supports prompts
    /// </summary>
    public bool SupportsPrompts { get; init; }

    /// <summary>
    /// Gets or sets the supports resources
    /// </summary>
    public bool SupportsResources { get; init; }

    /// <summary>
    /// Gets or sets the last discovery utc
    /// </summary>
    public DateTimeOffset? LastDiscoveryUtc { get; init; }
}
