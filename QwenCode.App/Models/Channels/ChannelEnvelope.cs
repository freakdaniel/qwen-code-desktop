namespace QwenCode.App.Models;

public sealed class ChannelEnvelope
{
    public required string ChannelName { get; init; }

    public required string SenderId { get; init; }

    public string SenderName { get; init; } = string.Empty;

    public required string ChatId { get; init; }

    public required string Text { get; init; }

    public string ThreadId { get; init; } = string.Empty;

    public string ReplyAddress { get; init; } = string.Empty;

    public bool IsGroup { get; init; }

    public bool IsMentioned { get; init; }

    public bool IsReplyToBot { get; init; }

    public string ReferencedText { get; init; } = string.Empty;

    public string ImageBase64 { get; init; } = string.Empty;

    public string ImageMimeType { get; init; } = string.Empty;

    public IReadOnlyList<ChannelAttachment> Attachments { get; init; } = [];
}
