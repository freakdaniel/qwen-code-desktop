namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Mcp Server Registration Request
/// </summary>
public sealed class McpServerRegistrationRequest
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
    public required string CommandOrUrl { get; init; }

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
    /// Gets or sets the server instructions
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
}
