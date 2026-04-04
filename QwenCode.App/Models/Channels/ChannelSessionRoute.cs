namespace QwenCode.App.Models;

public sealed class ChannelSessionRoute
{
    public required string SessionId { get; init; }

    public required string ChannelName { get; init; }

    public required string SenderId { get; init; }

    public required string ChatId { get; init; }

    public string ThreadId { get; init; } = string.Empty;

    public string ReplyAddress { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;
}
