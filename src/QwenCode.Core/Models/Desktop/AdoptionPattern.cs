namespace QwenCode.App.Models;

/// <summary>
/// Represents the Adoption Pattern
/// </summary>
public sealed class AdoptionPattern
{
    /// <summary>
    /// Gets or sets the area
    /// </summary>
    public required string Area { get; init; }

    /// <summary>
    /// Gets or sets the qwen source
    /// </summary>
    public required string QwenSource { get; init; }

    /// <summary>
    /// Gets or sets the claude reference
    /// </summary>
    public required string ClaudeReference { get; init; }

    /// <summary>
    /// Gets or sets the desktop direction
    /// </summary>
    public required string DesktopDirection { get; init; }

    /// <summary>
    /// Gets or sets the delivery state
    /// </summary>
    public required string DeliveryState { get; init; }
}
