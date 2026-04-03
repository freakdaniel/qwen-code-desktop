namespace QwenCode.App.Models;

public sealed class McpReconnectResult
{
    public required string Name { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset AttemptedAtUtc { get; init; }

    public string Message { get; init; } = string.Empty;

    public int DiscoveredToolsCount { get; init; }

    public int DiscoveredPromptsCount { get; init; }

    public bool SupportsPrompts { get; init; }

    public bool SupportsResources { get; init; }

    public DateTimeOffset? LastDiscoveryUtc { get; init; }
}
