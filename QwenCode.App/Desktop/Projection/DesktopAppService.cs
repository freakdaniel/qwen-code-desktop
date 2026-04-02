using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public sealed class DesktopAppService(
    ILocaleStateService localeStateService,
    IDesktopBootstrapProjectionService bootstrapProjectionService,
    IDesktopSessionProjectionService sessionProjectionService) : IDesktopProjectionService
{
    public event EventHandler<DesktopStateChangedEvent>? StateChanged;

    public event EventHandler<DesktopSessionEvent>? SessionEvent
    {
        add => sessionProjectionService.SessionEvent += value;
        remove => sessionProjectionService.SessionEvent -= value;
    }

    public Task<AppBootstrapPayload> GetBootstrapAsync() =>
        Task.FromResult(bootstrapProjectionService.CreateBootstrap(localeStateService.CurrentLocale));

    public Task<DesktopStateChangedEvent> SetLocaleAsync(string locale)
    {
        var state = localeStateService.SetLocale(locale);
        StateChanged?.Invoke(this, state);
        return Task.FromResult(state);
    }

    public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) =>
        sessionProjectionService.GetSessionAsync(request);

    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync() =>
        sessionProjectionService.GetActiveTurnsAsync();

    public Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) =>
        sessionProjectionService.ExecuteNativeToolAsync(request);

    public Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request) =>
        sessionProjectionService.StartSessionTurnAsync(request);

    public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request) =>
        sessionProjectionService.ApprovePendingToolAsync(request);

    public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) =>
        sessionProjectionService.CancelSessionTurnAsync(request);

    public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) =>
        sessionProjectionService.ResumeInterruptedTurnAsync(request);

    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) =>
        sessionProjectionService.DismissInterruptedTurnAsync(request);
}
