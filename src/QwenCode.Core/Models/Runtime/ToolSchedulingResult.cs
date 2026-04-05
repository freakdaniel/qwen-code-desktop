namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Tool Scheduling Result
/// </summary>
public sealed class ToolSchedulingResult
{
    /// <summary>
    /// Gets or sets the continue turn loop
    /// </summary>
    public bool ContinueTurnLoop { get; init; }

    /// <summary>
    /// Gets or sets the terminal summary
    /// </summary>
    public string TerminalSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the terminal stop reason
    /// </summary>
    public string TerminalStopReason { get; init; } = string.Empty;
}
