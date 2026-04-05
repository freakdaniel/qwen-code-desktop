namespace QwenCode.App.Models;

public sealed class ChannelPairingSnapshot
{
    public required string ChannelName { get; init; }

    public required int PendingCount { get; init; }

    public required int AllowlistCount { get; init; }

    public required IReadOnlyList<ChannelPairingRequest> PendingRequests { get; init; }
}
