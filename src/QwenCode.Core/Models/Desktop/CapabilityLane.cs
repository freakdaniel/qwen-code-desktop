namespace QwenCode.App.Models;

/// <summary>
/// Represents the Capability Lane
/// </summary>
public sealed class CapabilityLane
{
    /// <summary>
    /// Gets or sets the title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets or sets the responsibilities
    /// </summary>
    public required IReadOnlyList<string> Responsibilities { get; init; }
}
