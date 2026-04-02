using QwenCode.App.Ipc.Attributes;
using QwenCode.App.Desktop;
using QwenCode.App.Models;

namespace QwenCode.App.Ipc;

public sealed class DesktopIpcService(
    IServiceProvider services,
    IDesktopProjectionService desktopProjectionService) : IpcServiceBase(services)
{
    [IpcInvoke("qwen-desktop:app:bootstrap")]
    public Task<AppBootstrapPayload> Bootstrap()
        => desktopProjectionService.GetBootstrapAsync();

    [IpcInvoke("qwen-desktop:sessions:get")]
    public Task<DesktopSessionDetail?> GetSession(GetDesktopSessionRequest request)
        => desktopProjectionService.GetSessionAsync(request);

    [IpcInvoke("qwen-desktop:sessions:get-active-turns")]
    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurns()
        => desktopProjectionService.GetActiveTurnsAsync();

    [IpcInvoke("qwen-desktop:app:set-locale")]
    public Task<DesktopStateChangedEvent> SetLocale(SetLocaleRequest request)
        => desktopProjectionService.SetLocaleAsync(request.Locale);

    [IpcInvoke("qwen-desktop:tools:execute-native")]
    public Task<NativeToolExecutionResult> ExecuteNativeTool(ExecuteNativeToolRequest request)
        => desktopProjectionService.ExecuteNativeToolAsync(request);

    [IpcInvoke("qwen-desktop:sessions:start-turn")]
    public Task<DesktopSessionTurnResult> StartSessionTurn(StartDesktopSessionTurnRequest request)
        => desktopProjectionService.StartSessionTurnAsync(request);

    [IpcInvoke("qwen-desktop:sessions:approve-tool")]
    public Task<DesktopSessionTurnResult> ApprovePendingTool(ApproveDesktopSessionToolRequest request)
        => desktopProjectionService.ApprovePendingToolAsync(request);

    [IpcInvoke("qwen-desktop:sessions:cancel-turn")]
    public Task<CancelDesktopSessionTurnResult> CancelSessionTurn(CancelDesktopSessionTurnRequest request)
        => desktopProjectionService.CancelSessionTurnAsync(request);

    [IpcInvoke("qwen-desktop:sessions:resume-interrupted")]
    public Task<DesktopSessionTurnResult> ResumeInterruptedTurn(ResumeInterruptedTurnRequest request)
        => desktopProjectionService.ResumeInterruptedTurnAsync(request);

    [IpcInvoke("qwen-desktop:sessions:dismiss-interrupted")]
    public Task<DismissInterruptedTurnResult> DismissInterruptedTurn(DismissInterruptedTurnRequest request)
        => desktopProjectionService.DismissInterruptedTurnAsync(request);

    [IpcEvent("qwen-desktop:app:state-changed")]
    public void SubscribeStateChanged(Action<DesktopStateChangedEvent> emit)
    {
        desktopProjectionService.StateChanged += (_, state) => emit(state);
    }

    [IpcEvent("qwen-desktop:sessions:event")]
    public void SubscribeSessionEvents(Action<DesktopSessionEvent> emit)
    {
        desktopProjectionService.SessionEvent += (_, sessionEvent) => emit(sessionEvent);
    }
}
