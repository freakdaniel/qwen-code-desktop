namespace QwenCode.App.Models;

public sealed class McpServerRegistrationRequest
{
    public required string Name { get; init; }

    public required string Scope { get; init; }

    public required string Transport { get; init; }

    public required string CommandOrUrl { get; init; }

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
}
