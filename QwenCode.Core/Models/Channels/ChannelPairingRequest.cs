namespace QwenCode.App.Models;

public sealed class ChannelPairingRequest
{
    public required string SenderId { get; init; }

    public required string SenderName { get; init; }

    public required string Code { get; init; }

    public required string CreatedAtUtc { get; init; }

    public required int MinutesAgo { get; init; }
}
