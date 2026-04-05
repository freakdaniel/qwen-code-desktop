namespace QwenCode.App.Models;

/// <summary>
/// Represents the Channel Session Route
/// </summary>
public sealed class ChannelSessionRoute
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the channel name
    /// </summary>
    public required string ChannelName { get; init; }

    /// <summary>
    /// Gets or sets the sender id
    /// </summary>
    public required string SenderId { get; init; }

    /// <summary>
    /// Gets or sets the chat id
    /// </summary>
    public required string ChatId { get; init; }

    /// <summary>
    /// Gets or sets the thread id
    /// </summary>
    public string ThreadId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the reply address
    /// </summary>
    public string ReplyAddress { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;
}
