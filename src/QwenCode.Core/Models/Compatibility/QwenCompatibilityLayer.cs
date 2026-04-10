namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Qwen Compatibility Layer
/// </summary>
public sealed class QwenCompatibilityLayer
{
    /// <summary>
    /// Gets or sets the id
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the priority
    /// </summary>
    public required int Priority { get; init; }

    /// <summary>
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the exists
    /// </summary>
    public required bool Exists { get; init; }

    /// <summary>
    /// Gets or sets the categories
    /// </summary>
    public required IReadOnlyList<string> Categories { get; init; }
}
