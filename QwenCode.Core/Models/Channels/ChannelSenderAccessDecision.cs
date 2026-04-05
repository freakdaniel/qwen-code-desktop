namespace QwenCode.App.Models;

public sealed class ChannelSenderAccessDecision
{
    public bool Allowed { get; init; }

    public string PairingCode { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}
