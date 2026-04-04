namespace QwenCode.App.Models;

public sealed class ExtensionConsentSnapshot
{
    public required string Name { get; init; }

    public required string InstallType { get; init; }

    public required string Source { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }

    public IReadOnlyList<string> Commands { get; init; } = [];

    public IReadOnlyList<string> Skills { get; init; } = [];

    public IReadOnlyList<string> Agents { get; init; } = [];

    public IReadOnlyList<string> McpServers { get; init; } = [];

    public IReadOnlyList<string> Channels { get; init; } = [];

    public IReadOnlyList<string> ContextFiles { get; init; } = [];
}
