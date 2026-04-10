namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Channel Envelope
/// </summary>
public sealed class ChannelEnvelope
{
    /// <summary>
    /// Gets or sets the channel name
    /// </summary>
    public required string ChannelName { get; init; }

    /// <summary>
    /// Gets or sets the sender id
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Gets or sets the sender name
    /// </summary>
    public string SenderName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the chat id
    /// </summary>
    public required string ChatId { get; init; }

    /// <summary>
    /// Gets or sets the text
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the thread id
    /// </summary>
    public string ThreadId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the reply address
    /// </summary>
    public string ReplyAddress { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether is group
    /// </summary>
    public bool IsGroup { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is mentioned
    /// </summary>
    public bool IsMentioned { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is reply to bot
    /// </summary>
    public bool IsReplyToBot { get; init; }

    /// <summary>
    /// Gets or sets the referenced text
    /// </summary>
    public string ReferencedText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the image base64
    /// </summary>
    public string ImageBase64 { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the image mime type
    /// </summary>
    public string ImageMimeType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the attachments
    /// </summary>
    public IReadOnlyList<ChannelAttachment> Attachments { get; init; } = [];
}
