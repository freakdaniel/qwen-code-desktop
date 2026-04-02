namespace QwenCode.App.Models;

public sealed class ProjectSummarySnapshot
{
    public bool HasHistory { get; init; }

    public required string FilePath { get; init; }

    public required string Content { get; init; }

    public string TimestampText { get; init; } = string.Empty;

    public string TimeAgo { get; init; } = string.Empty;

    public required string OverallGoal { get; init; }

    public required string CurrentPlan { get; init; }

    public int TotalTasks { get; init; }

    public int DoneCount { get; init; }

    public int InProgressCount { get; init; }

    public int TodoCount { get; init; }

    public required IReadOnlyList<string> PendingTasks { get; init; }

    public required DateTime TimestampUtc { get; init; }
}
