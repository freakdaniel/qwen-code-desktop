namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Channel Dispatch Result
/// </summary>
public sealed class ChannelDispatchResult
{
    /// <summary>
    /// Gets or sets the channel name
    /// </summary>
    public required string ChannelName { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the pairing code
    /// </summary>
    public string PairingCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public string TranscriptPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the created new session
    /// </summary>
    public bool CreatedNewSession { get; init; }

    /// <summary>
    /// Gets or sets the assistant summary
    /// </summary>
    public string AssistantSummary { get; init; } = string.Empty;
}
