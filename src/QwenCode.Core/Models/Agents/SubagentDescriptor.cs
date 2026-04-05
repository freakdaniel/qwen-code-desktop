namespace QwenCode.App.Models;

/// <summary>
/// Represents the Subagent Descriptor
/// </summary>
public sealed class SubagentDescriptor
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the system prompt
    /// </summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether is builtin
    /// </summary>
    public bool IsBuiltin { get; init; }

    /// <summary>
    /// Gets or sets the tools
    /// </summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the color
    /// </summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the run configuration
    /// </summary>
    public SubagentRunConfiguration RunConfiguration { get; init; } = new();

    /// <summary>
    /// Gets or sets the validation warnings
    /// </summary>
    public IReadOnlyList<string> ValidationWarnings { get; init; } = [];
}
