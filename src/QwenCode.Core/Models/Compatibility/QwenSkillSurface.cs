namespace QwenCode.App.Models;

/// <summary>
/// Represents the Qwen Skill Surface
/// </summary>
public sealed class QwenSkillSurface
{
    /// <summary>
    /// Gets or sets the id
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the allowed tools
    /// </summary>
    public required IReadOnlyList<string> AllowedTools { get; init; }
}
