namespace QwenCode.App.Models;

public sealed class ChannelOutboundMessage
{
    public required string ChannelName { get; init; }

    public required string SessionId { get; init; }

    public required string ChatId { get; init; }

    public required string SenderId { get; init; }

    public required string Kind { get; init; }

    public required string Text { get; init; }

    public string ToolName { get; init; } = string.Empty;

    public string CommandName { get; init; } = string.Empty;

    public required DateTime TimestampUtc { get; init; }
}
