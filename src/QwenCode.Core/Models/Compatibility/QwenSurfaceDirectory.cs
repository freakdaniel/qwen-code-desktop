namespace QwenCode.App.Models;

/// <summary>
/// Represents the Qwen Surface Directory
/// </summary>
public sealed class QwenSurfaceDirectory
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
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the exists
    /// </summary>
    public required bool Exists { get; init; }

    /// <summary>
    /// Gets or sets the item count
    /// </summary>
    public required int ItemCount { get; init; }

    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public required string Summary { get; init; }
}
