namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Resolved Command
/// </summary>
public sealed class ResolvedCommand
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the source path
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the arguments
    /// </summary>
    public required string Arguments { get; init; }

    /// <summary>
    /// Gets or sets the resolved prompt
    /// </summary>
    public required string ResolvedPrompt { get; init; }
}
