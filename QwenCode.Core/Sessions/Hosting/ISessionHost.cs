using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface ISessionHost
{
    event EventHandler<DesktopSessionEvent>? SessionEvent;

    Task<DesktopSessionTurnResult> StartTurnAsync(
        WorkspacePaths paths,
        StartDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
        WorkspacePaths paths,
        ApproveDesktopSessionToolRequest request,
        CancellationToken cancellationToken = default);

    Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
        WorkspacePaths paths,
        AnswerDesktopSessionQuestionRequest request,
        CancellationToken cancellationToken = default);

    Task<CancelDesktopSessionTurnResult> CancelTurnAsync(
        WorkspacePaths paths,
        CancelDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(
        WorkspacePaths paths,
        ResumeInterruptedTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(
        WorkspacePaths paths,
        DismissInterruptedTurnRequest request,
        CancellationToken cancellationToken = default);
}
