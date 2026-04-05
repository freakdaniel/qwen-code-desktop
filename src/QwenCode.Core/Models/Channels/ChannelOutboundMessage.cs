namespace QwenCode.App.Models;

/// <summary>
/// Represents the Channel Outbound Message
/// </summary>
public sealed class ChannelOutboundMessage
{
    /// <summary>
    /// Gets or sets the channel name
    /// </summary>
    public required string ChannelName { get; init; }

    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the chat id
    /// </summary>
    public required string ChatId { get; init; }

    /// <summary>
    /// Gets or sets the sender id
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Gets or sets the kind
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets or sets the text
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the command name
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public required DateTime TimestampUtc { get; init; }
}
