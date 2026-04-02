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

    [IpcInvoke("qwen-desktop:auth:status")]
    public Task<AuthStatusSnapshot> GetAuthStatus()
        => desktopProjectionService.GetAuthStatusAsync();

    [IpcInvoke("qwen-desktop:auth:configure-openai-compatible")]
    public Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAuth(ConfigureOpenAiCompatibleAuthRequest request)
        => desktopProjectionService.ConfigureOpenAiCompatibleAuthAsync(request);

    [IpcInvoke("qwen-desktop:auth:configure-coding-plan")]
    public Task<AuthStatusSnapshot> ConfigureCodingPlanAuth(ConfigureCodingPlanAuthRequest request)
        => desktopProjectionService.ConfigureCodingPlanAuthAsync(request);

    [IpcInvoke("qwen-desktop:auth:configure-qwen-oauth")]
    public Task<AuthStatusSnapshot> ConfigureQwenOAuth(ConfigureQwenOAuthRequest request)
        => desktopProjectionService.ConfigureQwenOAuthAsync(request);

    [IpcInvoke("qwen-desktop:auth:start-qwen-oauth-device-flow")]
    public Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlow(StartQwenOAuthDeviceFlowRequest request)
        => desktopProjectionService.StartQwenOAuthDeviceFlowAsync(request);

    [IpcInvoke("qwen-desktop:auth:cancel-qwen-oauth-device-flow")]
    public Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlow(CancelQwenOAuthDeviceFlowRequest request)
        => desktopProjectionService.CancelQwenOAuthDeviceFlowAsync(request);

    [IpcInvoke("qwen-desktop:auth:disconnect")]
    public Task<AuthStatusSnapshot> DisconnectAuth(DisconnectAuthRequest request)
        => desktopProjectionService.DisconnectAuthAsync(request);

    [IpcInvoke("qwen-desktop:mcp:add")]
    public Task<McpSnapshot> AddMcpServer(McpServerRegistrationRequest request)
        => desktopProjectionService.AddMcpServerAsync(request);

    [IpcInvoke("qwen-desktop:mcp:remove")]
    public Task<McpSnapshot> RemoveMcpServer(RemoveMcpServerRequest request)
        => desktopProjectionService.RemoveMcpServerAsync(request);

    [IpcInvoke("qwen-desktop:mcp:reconnect")]
    public Task<McpSnapshot> ReconnectMcpServer(ReconnectMcpServerRequest request)
        => desktopProjectionService.ReconnectMcpServerAsync(request);

    [IpcInvoke("qwen-desktop:tools:execute-native")]
    public Task<NativeToolExecutionResult> ExecuteNativeTool(ExecuteNativeToolRequest request)
        => desktopProjectionService.ExecuteNativeToolAsync(request);

    [IpcInvoke("qwen-desktop:sessions:start-turn")]
    public Task<DesktopSessionTurnResult> StartSessionTurn(StartDesktopSessionTurnRequest request)
        => desktopProjectionService.StartSessionTurnAsync(request);

    [IpcInvoke("qwen-desktop:sessions:approve-tool")]
    public Task<DesktopSessionTurnResult> ApprovePendingTool(ApproveDesktopSessionToolRequest request)
        => desktopProjectionService.ApprovePendingToolAsync(request);

    [IpcInvoke("qwen-desktop:sessions:answer-question")]
    public Task<DesktopSessionTurnResult> AnswerPendingQuestion(AnswerDesktopSessionQuestionRequest request)
        => desktopProjectionService.AnswerPendingQuestionAsync(request);

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

    [IpcEvent("qwen-desktop:auth:changed")]
    public void SubscribeAuthChanged(Action<AuthStatusSnapshot> emit)
    {
        desktopProjectionService.AuthChanged += (_, state) => emit(state);
    }

    [IpcEvent("qwen-desktop:sessions:event")]
    public void SubscribeSessionEvents(Action<DesktopSessionEvent> emit)
    {
        desktopProjectionService.SessionEvent += (_, sessionEvent) => emit(sessionEvent);
    }
}
