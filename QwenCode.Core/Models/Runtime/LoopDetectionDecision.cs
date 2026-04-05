namespace QwenCode.App.Models;

public sealed class LoopDetectionDecision
{
    public bool IsDetected { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string LoopType { get; init; } = string.Empty;
}
