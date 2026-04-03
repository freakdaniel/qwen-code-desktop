namespace QwenCode.App.Models;

public sealed class McpServerDefinition
{
    public required string Name { get; init; }

    public required string Scope { get; init; }

    public required string Transport { get; init; }

    public string CommandOrUrl { get; init; } = string.Empty;

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public int? TimeoutMs { get; init; }

    public bool Trust { get; init; }

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> IncludeTools { get; init; } = [];

    public IReadOnlyList<string> ExcludeTools { get; init; } = [];

    public string SettingsPath { get; init; } = string.Empty;

    public string Status { get; init; } = "unknown";

    public DateTimeOffset? LastReconnectAttemptUtc { get; init; }

    public string LastError { get; init; } = string.Empty;

    public bool HasPersistedToken { get; init; }

    public int DiscoveredToolsCount { get; init; }

    public int DiscoveredPromptsCount { get; init; }

    public bool SupportsPrompts { get; init; }

    public bool SupportsResources { get; init; }

    public DateTimeOffset? LastDiscoveryUtc { get; init; }
}
