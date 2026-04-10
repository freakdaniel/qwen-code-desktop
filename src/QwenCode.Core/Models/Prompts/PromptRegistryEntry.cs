namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Prompt Registry Entry
/// </summary>
public sealed class PromptRegistryEntry
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the prompt name
    /// </summary>
    public required string PromptName { get; init; }

    /// <summary>
    /// Gets or sets the qualified name
    /// </summary>
    public required string QualifiedName { get; init; }

    /// <summary>
    /// Gets or sets the server name
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments json
    /// </summary>
    public string ArgumentsJson { get; init; } = "[]";

    /// <summary>
    /// Gets or sets the source
    /// </summary>
    public string Source { get; init; } = "mcp";
}
