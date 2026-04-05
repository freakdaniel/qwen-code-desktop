namespace QwenCode.App.Models;

public sealed class ApprovalDecision
{
    public required string State { get; init; }

    public required string Reason { get; init; }
}
