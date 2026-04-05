namespace QwenCode.App.Models;

public sealed class ExtensionDefinition
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string Path { get; init; }

    public required string WrapperPath { get; init; }

    public required string Status { get; init; }

    public required string InstallType { get; init; }

    public required string Source { get; init; }

    public required bool UserEnabled { get; init; }

    public required bool WorkspaceEnabled { get; init; }

    public required bool IsActive { get; init; }

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> ContextFiles { get; init; } = [];

    public IReadOnlyList<string> Commands { get; init; } = [];

    public IReadOnlyList<string> Skills { get; init; } = [];

    public IReadOnlyList<string> Agents { get; init; } = [];

    public IReadOnlyList<string> McpServers { get; init; } = [];

    public IReadOnlyList<string> Channels { get; init; } = [];

    public int SettingsCount { get; init; }

    public int HookEventCount { get; init; }

    public string LastError { get; init; } = string.Empty;
}
