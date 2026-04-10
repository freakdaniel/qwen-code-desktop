namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Arena Session Event
/// </summary>
public sealed class ArenaSessionEvent
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind
    /// </summary>
    public ArenaSessionEventKind Kind { get; init; }

    /// <summary>
    /// Gets or sets the linked orchestration task id
    /// </summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the round count
    /// </summary>
    public int RoundCount { get; init; }

    /// <summary>
    /// Gets or sets the selected winner
    /// </summary>
    public string SelectedWinner { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stats
    /// </summary>
    public ArenaSessionStats Stats { get; init; } = new();

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public DateTime TimestampUtc { get; init; }
}
