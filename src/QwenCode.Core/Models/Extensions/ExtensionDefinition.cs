namespace QwenCode.App.Models;

/// <summary>
/// Represents the Extension Definition
/// </summary>
public sealed class ExtensionDefinition
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the version
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the wrapper path
    /// </summary>
    public required string WrapperPath { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the install type
    /// </summary>
    public required string InstallType { get; init; }

    /// <summary>
    /// Gets or sets the source
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets or sets the user enabled
    /// </summary>
    public required bool UserEnabled { get; init; }

    /// <summary>
    /// Gets or sets the workspace enabled
    /// </summary>
    public required bool WorkspaceEnabled { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is active
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the context files
    /// </summary>
    public IReadOnlyList<string> ContextFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets the commands
    /// </summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>
    /// Gets or sets the skills
    /// </summary>
    public IReadOnlyList<string> Skills { get; init; } = [];

    /// <summary>
    /// Gets or sets the agents
    /// </summary>
    public IReadOnlyList<string> Agents { get; init; } = [];

    /// <summary>
    /// Gets or sets the mcp servers
    /// </summary>
    public IReadOnlyList<string> McpServers { get; init; } = [];

    /// <summary>
    /// Gets or sets the channels
    /// </summary>
    public IReadOnlyList<string> Channels { get; init; } = [];

    /// <summary>
    /// Gets or sets the settings count
    /// </summary>
    public int SettingsCount { get; init; }

    /// <summary>
    /// Gets or sets the hook event count
    /// </summary>
    public int HookEventCount { get; init; }

    /// <summary>
    /// Gets or sets the last error
    /// </summary>
    public string LastError { get; init; } = string.Empty;
}
