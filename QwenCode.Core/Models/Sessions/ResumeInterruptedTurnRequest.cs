namespace QwenCode.App.Models;

public sealed class ResumeInterruptedTurnRequest
{
    public required string SessionId { get; init; }

    public string RecoveryNote { get; init; } = string.Empty;
}
