using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public sealed class DesktopAppService(
    ILocaleStateService localeStateService,
    IDesktopBootstrapProjectionService bootstrapProjectionService,
    IDesktopAuthProjectionService authProjectionService,
    IDesktopMcpProjectionService mcpProjectionService,
    IDesktopSessionProjectionService sessionProjectionService) : IDesktopProjectionService
{
    public event EventHandler<DesktopStateChangedEvent>? StateChanged;

    public event EventHandler<AuthStatusSnapshot>? AuthChanged
    {
        add => authProjectionService.AuthChanged += value;
        remove => authProjectionService.AuthChanged -= value;
    }

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

    public Task<AuthStatusSnapshot> GetAuthStatusAsync() =>
        Task.FromResult(authProjectionService.CreateSnapshot());

    public Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAuthAsync(ConfigureOpenAiCompatibleAuthRequest request) =>
        authProjectionService.ConfigureOpenAiCompatibleAsync(request);

    public Task<AuthStatusSnapshot> ConfigureCodingPlanAuthAsync(ConfigureCodingPlanAuthRequest request) =>
        authProjectionService.ConfigureCodingPlanAsync(request);

    public Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(ConfigureQwenOAuthRequest request) =>
        authProjectionService.ConfigureQwenOAuthAsync(request);

    public Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(StartQwenOAuthDeviceFlowRequest request) =>
        authProjectionService.StartQwenOAuthDeviceFlowAsync(request);

    public Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(CancelQwenOAuthDeviceFlowRequest request) =>
        authProjectionService.CancelQwenOAuthDeviceFlowAsync(request);

    public Task<AuthStatusSnapshot> DisconnectAuthAsync(DisconnectAuthRequest request) =>
        authProjectionService.DisconnectAsync(request);

    public Task<McpSnapshot> AddMcpServerAsync(McpServerRegistrationRequest request) =>
        mcpProjectionService.AddServerAsync(request);

    public Task<McpSnapshot> RemoveMcpServerAsync(RemoveMcpServerRequest request) =>
        mcpProjectionService.RemoveServerAsync(request);

    public Task<McpSnapshot> ReconnectMcpServerAsync(ReconnectMcpServerRequest request) =>
        mcpProjectionService.ReconnectServerAsync(request);

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

    public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request) =>
        sessionProjectionService.AnswerPendingQuestionAsync(request);

    public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) =>
        sessionProjectionService.CancelSessionTurnAsync(request);

    public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) =>
        sessionProjectionService.ResumeInterruptedTurnAsync(request);

    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) =>
        sessionProjectionService.DismissInterruptedTurnAsync(request);
}
