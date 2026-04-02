using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopProjectionService
{
    event EventHandler<DesktopStateChangedEvent>? StateChanged;

    event EventHandler<DesktopSessionEvent>? SessionEvent;

    Task<AppBootstrapPayload> GetBootstrapAsync();

    Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync();

    Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request);

    Task<DesktopStateChangedEvent> SetLocaleAsync(string locale);

    Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request);

    Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request);

    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request);

    Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request);

    Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request);

    Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request);
}
