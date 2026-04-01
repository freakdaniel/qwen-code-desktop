using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface ISessionHost
{
    Task<DesktopSessionTurnResult> StartTurnAsync(
        SourceMirrorPaths paths,
        StartDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
        SourceMirrorPaths paths,
        ApproveDesktopSessionToolRequest request,
        CancellationToken cancellationToken = default);
}
