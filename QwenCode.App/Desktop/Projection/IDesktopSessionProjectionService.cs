using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopSessionProjectionService
{
    event EventHandler<DesktopSessionEvent>? SessionEvent;

    Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync();

    Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request);

    Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request);

    Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request);

    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request);

    Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request);

    Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request);

    Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request);

    Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request);
}
