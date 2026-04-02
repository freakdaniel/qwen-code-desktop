namespace QwenCode.App.Models;

public sealed class ScheduledCronJob
{
    public required string Id { get; init; }

    public required string CronExpression { get; init; }

    public required string Prompt { get; init; }

    public required bool IsRecurring { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? LastFiredAt { get; set; }

    public required int JitterMilliseconds { get; init; }
}
