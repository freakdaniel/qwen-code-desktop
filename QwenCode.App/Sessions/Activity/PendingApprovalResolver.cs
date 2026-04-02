using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public sealed class PendingApprovalResolver : IPendingApprovalResolver
{
    public DesktopSessionEntry ResolvePendingTool(DesktopSessionDetail detail, string? entryId) =>
        detail.Entries
            .Where(static entry =>
                entry.Type == "tool" &&
                string.Equals(entry.Status, "approval-required", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(entry.ResolutionStatus))
            .LastOrDefault(entry => string.IsNullOrWhiteSpace(entryId) || string.Equals(entry.Id, entryId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException("No pending tool approval was found for this session.");
}
