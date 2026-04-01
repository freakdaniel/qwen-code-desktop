namespace QwenCode.App.Models;

public sealed class QwenApprovalDecision
{
    public required string State { get; init; }

    public required string Reason { get; init; }
}
