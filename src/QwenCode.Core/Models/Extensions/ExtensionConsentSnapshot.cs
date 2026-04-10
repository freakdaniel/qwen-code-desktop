namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Extension Consent Snapshot
/// </summary>
public sealed class ExtensionConsentSnapshot
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the install type
    /// </summary>
    public required string InstallType { get; init; }

    /// <summary>
    /// Gets or sets the source
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets or sets the warnings
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

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
    /// Gets or sets the context files
    /// </summary>
    public IReadOnlyList<string> ContextFiles { get; init; } = [];
}
