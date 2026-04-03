namespace QwenCode.App.Models;

public sealed class ApproveChannelPairingRequest
{
    public required string Name { get; init; }

    public required string Code { get; init; }
}
