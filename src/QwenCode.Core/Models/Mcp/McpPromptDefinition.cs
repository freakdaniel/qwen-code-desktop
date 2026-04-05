namespace QwenCode.App.Models;

/// <summary>
/// Represents the Mcp Prompt Definition
/// </summary>
public sealed class McpPromptDefinition
{
    /// <summary>
    /// Gets or sets the server name
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments json
    /// </summary>
    public string ArgumentsJson { get; init; } = "[]";
}
