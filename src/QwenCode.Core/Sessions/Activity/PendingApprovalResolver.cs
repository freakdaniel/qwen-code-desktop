using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

/// <summary>
/// Represents the Pending Approval Resolver
/// </summary>
public sealed class PendingApprovalResolver : IPendingApprovalResolver
{
    /// <summary>
    /// Resolves pending tool
    /// </summary>
    /// <param name="detail">The detail</param>
    /// <param name="entryId">The entry id</param>
    /// <returns>The resulting desktop session entry</returns>
    public DesktopSessionEntry ResolvePendingTool(DesktopSessionDetail detail, string? entryId) =>
        detail.Entries
            .Where(static entry =>
                entry.Type == "tool" &&
                string.Equals(entry.Status, "approval-required", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(entry.ResolutionStatus))
            .LastOrDefault(entry => string.IsNullOrWhiteSpace(entryId) || string.Equals(entry.Id, entryId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException("No pending tool approval was found for this session.");

    /// <summary>
    /// Resolves pending question
    /// </summary>
    /// <param name="detail">The detail</param>
    /// <param name="entryId">The entry id</param>
    /// <returns>The resulting desktop session entry</returns>
    public DesktopSessionEntry ResolvePendingQuestion(DesktopSessionDetail detail, string? entryId) =>
        detail.Entries
            .Where(static entry =>
                entry.Type == "tool" &&
                string.Equals(entry.ToolName, "ask_user_question", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Status, "input-required", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(entry.ResolutionStatus))
            .LastOrDefault(entry => string.IsNullOrWhiteSpace(entryId) || string.Equals(entry.Id, entryId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException("No pending user question was found for this session.");
}
