namespace QwenCode.App.Models;

public sealed class McpSnapshot
{
    public required int TotalCount { get; init; }

    public required int ConnectedCount { get; init; }

    public required int DisconnectedCount { get; init; }

    public required int MissingCount { get; init; }

    public required int TokenCount { get; init; }

    public required IReadOnlyList<McpServerDefinition> Servers { get; init; }
}
