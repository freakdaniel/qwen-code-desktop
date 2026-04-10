namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Hook Output
/// </summary>
public sealed class HookOutput
{
    /// <summary>
    /// Gets or sets the continue
    /// </summary>
    public bool? Continue { get; init; }

    /// <summary>
    /// Gets or sets the stop reason
    /// </summary>
    public string StopReason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the decision
    /// </summary>
    public string Decision { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the reason
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the system message
    /// </summary>
    public string SystemMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the additional context
    /// </summary>
    public string AdditionalContext { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the modified prompt
    /// </summary>
    public string ModifiedPrompt { get; init; } = string.Empty;
}
