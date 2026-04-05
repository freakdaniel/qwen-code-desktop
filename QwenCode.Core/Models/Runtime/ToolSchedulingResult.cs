namespace QwenCode.App.Runtime;

public sealed class ToolSchedulingResult
{
    public bool ContinueTurnLoop { get; init; }

    public string TerminalSummary { get; init; } = string.Empty;

    public string TerminalStopReason { get; init; } = string.Empty;
}
