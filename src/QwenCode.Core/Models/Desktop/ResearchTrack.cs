namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Research Track
/// </summary>
public sealed class ResearchTrack
{
    /// <summary>
    /// Gets or sets the title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public required string Summary { get; init; }
}
