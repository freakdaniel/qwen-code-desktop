using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

internal static class DesktopSessionActivitySummaryBuilder
{
    /// <summary>
    /// Builds value
    /// </summary>
    /// <param name="entries">The entries</param>
    /// <returns>The resulting desktop session activity summary</returns>
    public static DesktopSessionActivitySummary Build(IReadOnlyList<DesktopSessionEntry> entries) => new()
    {
        UserCount = entries.Count(static entry => entry.Type == "user"),
        AssistantCount = entries.Count(static entry => entry.Type == "assistant"),
        CommandCount = entries.Count(static entry => entry.Type == "command"),
        ToolCount = entries.Count(static entry => entry.Type == "tool"),
        PendingApprovalCount = entries.Count(static entry =>
            entry.Type == "tool" &&
            string.Equals(entry.Status, "approval-required", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(entry.ResolutionStatus)),
        PendingQuestionCount = entries.Count(static entry =>
            entry.Type == "tool" &&
            string.Equals(entry.Status, "input-required", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(entry.ResolutionStatus)),
        CompletedToolCount = entries.Count(static entry =>
            entry.Type == "tool" && string.Equals(entry.Status, "completed", StringComparison.OrdinalIgnoreCase)),
        FailedToolCount = entries.Count(static entry =>
            entry.Type == "tool" &&
            !string.IsNullOrWhiteSpace(entry.Status) &&
            !string.Equals(entry.Status, "completed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Status, "approval-required", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.Status, "input-required", StringComparison.OrdinalIgnoreCase)),
        LastTimestamp = entries.LastOrDefault()?.Timestamp ?? string.Empty
    };
}
