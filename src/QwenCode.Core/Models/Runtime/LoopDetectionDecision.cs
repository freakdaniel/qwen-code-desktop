namespace QwenCode.App.Models;

/// <summary>
/// Represents the Loop Detection Decision
/// </summary>
public sealed class LoopDetectionDecision
{
    /// <summary>
    /// Gets or sets a value indicating whether is detected
    /// </summary>
    public bool IsDetected { get; init; }

    /// <summary>
    /// Gets or sets the reason
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the loop type
    /// </summary>
    public string LoopType { get; init; } = string.Empty;
}
