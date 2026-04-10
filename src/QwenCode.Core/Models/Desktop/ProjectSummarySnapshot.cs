namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Project Summary Snapshot
/// </summary>
public sealed class ProjectSummarySnapshot
{
    /// <summary>
    /// Gets or sets a value indicating whether has history
    /// </summary>
    public bool HasHistory { get; init; }

    /// <summary>
    /// Gets or sets the file path
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets or sets the content
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets or sets the timestamp text
    /// </summary>
    public string TimestampText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the time ago
    /// </summary>
    public string TimeAgo { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the overall goal
    /// </summary>
    public required string OverallGoal { get; init; }

    /// <summary>
    /// Gets or sets the current plan
    /// </summary>
    public required string CurrentPlan { get; init; }

    /// <summary>
    /// Gets or sets the total tasks
    /// </summary>
    public int TotalTasks { get; init; }

    /// <summary>
    /// Gets or sets the done count
    /// </summary>
    public int DoneCount { get; init; }

    /// <summary>
    /// Gets or sets the in progress count
    /// </summary>
    public int InProgressCount { get; init; }

    /// <summary>
    /// Gets or sets the todo count
    /// </summary>
    public int TodoCount { get; init; }

    /// <summary>
    /// Gets or sets the pending tasks
    /// </summary>
    public required IReadOnlyList<string> PendingTasks { get; init; }

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public required DateTime TimestampUtc { get; init; }
}
