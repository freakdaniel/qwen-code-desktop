namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Scheduled Cron Job
/// </summary>
public sealed class ScheduledCronJob
{
    /// <summary>
    /// Gets or sets the id
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the cron expression
    /// </summary>
    public required string CronExpression { get; init; }

    /// <summary>
    /// Gets or sets the prompt
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is recurring
    /// </summary>
    public required bool IsRecurring { get; init; }

    /// <summary>
    /// Gets or sets the created at
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets or sets the expires at
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets or sets the last fired at
    /// </summary>
    public DateTimeOffset? LastFiredAt { get; set; }

    /// <summary>
    /// Gets or sets the jitter milliseconds
    /// </summary>
    public required int JitterMilliseconds { get; init; }
}
