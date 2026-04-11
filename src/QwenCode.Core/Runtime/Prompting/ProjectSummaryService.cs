using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Project Summary Service
/// </summary>
public sealed partial class ProjectSummaryService : IProjectSummaryService
{
    private const string SummaryFileName = "PROJECT_SUMMARY.md";

    /// <summary>
    /// Reads value
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <returns>The resulting project summary snapshot?</returns>
    public ProjectSummarySnapshot? Read(QwenRuntimeProfile runtimeProfile)
    {
        if (string.IsNullOrWhiteSpace(runtimeProfile.ProjectRoot) || !runtimeProfile.IsWorkspaceTrusted)
        {
            return null;
        }

        var summaryPath = Path.Combine(Path.GetFullPath(runtimeProfile.ProjectRoot), ".qwen", SummaryFileName);
        if (!File.Exists(summaryPath))
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(summaryPath).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var timestampText = ResolveTimestampText(content);
            var timestampUtc = ResolveTimestampUtc(content, summaryPath);
            var currentPlan = ExtractSection(content, "Current Plan");
            var planLines = ExtractPlanLines(currentPlan);
            var doneCount = CountTasks(planLines, "[DONE]");
            var inProgressCount = CountTasks(planLines, "[IN PROGRESS]");
            var todoCount = CountTasks(planLines, "[TODO]");

            return new ProjectSummarySnapshot
            {
                HasHistory = true,
                FilePath = summaryPath,
                Content = content,
                TimestampText = timestampText,
                TimeAgo = FormatRelativeTime(timestampUtc, DateTime.UtcNow),
                OverallGoal = ExtractSection(content, "Overall Goal"),
                CurrentPlan = currentPlan,
                TotalTasks = doneCount + inProgressCount + todoCount,
                DoneCount = doneCount,
                InProgressCount = inProgressCount,
                TodoCount = todoCount,
                PendingTasks = ExtractPendingTasks(currentPlan),
                TimestampUtc = timestampUtc
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveTimestampText(string content)
    {
        var timestampMatch = UpdateTimeRegex().Match(content);
        return timestampMatch.Success
            ? timestampMatch.Groups["timestamp"].Value.Trim()
            : string.Empty;
    }

    private static DateTime ResolveTimestampUtc(string content, string summaryPath)
    {
        var timestampText = ResolveTimestampText(content);
        if (!string.IsNullOrWhiteSpace(timestampText) &&
            DateTime.TryParse(timestampText, out var parsedTimestamp))
        {
            return parsedTimestamp.Kind == DateTimeKind.Utc
                ? parsedTimestamp
                : parsedTimestamp.ToUniversalTime();
        }

        return File.GetLastWriteTimeUtc(summaryPath);
    }

    private static string ExtractSection(string content, string sectionName)
    {
        var match = Regex.Match(
            content,
            $@"## {Regex.Escape(sectionName)}\s*\r?\n(?<body>[\s\S]*?)(?=\r?\n## |\z)",
            RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups["body"].Value.Trim()
            : string.Empty;
    }

    private static IReadOnlyList<string> ExtractPlanLines(string currentPlan) =>
        currentPlan
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    private static int CountTasks(IReadOnlyList<string> planLines, string marker) =>
        planLines.Count(line => line.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> ExtractPendingTasks(string currentPlan) =>
        ExtractPlanLines(currentPlan)
            .Where(static line =>
                line.Contains("[TODO]", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("[IN PROGRESS]", StringComparison.OrdinalIgnoreCase))
            .Select(static line => PlanPrefixRegex().Replace(line, string.Empty).Trim())
            .Take(3)
            .ToArray();

    private static string FormatRelativeTime(DateTime timestampUtc, DateTime nowUtc)
    {
        var delta = nowUtc - timestampUtc;
        if (delta < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)delta.TotalMinutes);
            return $"{minutes} minute{Pluralize(minutes)} ago";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            var hours = Math.Max(1, (int)delta.TotalHours);
            return $"{hours} hour{Pluralize(hours)} ago";
        }

        var days = Math.Max(1, (int)delta.TotalDays);
        return $"{days} day{Pluralize(days)} ago";
    }

    private static string Pluralize(int value) => value == 1 ? string.Empty : "s";

    [GeneratedRegex(@"\*\*Update time\*\*:\s*(?<timestamp>.+)", RegexOptions.IgnoreCase)]
    private static partial Regex UpdateTimeRegex();

    [GeneratedRegex(@"^\d+\.\s*", RegexOptions.Compiled)]
    private static partial Regex PlanPrefixRegex();
}
