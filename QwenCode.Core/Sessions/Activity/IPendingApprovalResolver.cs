using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface IPendingApprovalResolver
{
    DesktopSessionEntry ResolvePendingTool(DesktopSessionDetail detail, string? entryId);

    DesktopSessionEntry ResolvePendingQuestion(DesktopSessionDetail detail, string? entryId);
}
