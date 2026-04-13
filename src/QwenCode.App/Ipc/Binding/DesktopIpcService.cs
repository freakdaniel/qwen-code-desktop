#pragma warning disable CS1591
using QwenCode.App.AppHost;
using QwenCode.App.Desktop;
using QwenCode.App.Desktop.DirectConnect;
using QwenCode.App.Ipc.Attributes;
using QwenCode.Core.Models;

namespace QwenCode.App.Ipc;

/// <summary>
/// Represents the desktop IPC surface exposed to the renderer bridge.
/// </summary>
/// <param name="services">The services.</param>
/// <param name="desktopProjectionService">The desktop projection service.</param>
/// <param name="directConnectSessionService">The direct-connect session service.</param>
/// <param name="directConnectServerHost">The direct-connect server host.</param>
/// <param name="desktopWindowBridge">The native window bridge.</param>
public sealed class DesktopIpcService(
    IServiceProvider services,
    IDesktopProjectionService desktopProjectionService,
    IDirectConnectSessionService directConnectSessionService,
    IDirectConnectServerHost directConnectServerHost,
    IDesktopWindowBridge desktopWindowBridge) : IpcServiceBase(services)
{
    [IpcInvoke("qwen-desktop:app:bootstrap")]
    public Task<AppBootstrapPayload> Bootstrap()
        => desktopProjectionService.GetBootstrapAsync();

    [IpcInvoke("qwen-desktop:sessions:get")]
    public Task<DesktopSessionDetail?> GetSession(GetDesktopSessionRequest request)
        => desktopProjectionService.GetSessionAsync(request);

    [IpcInvoke("qwen-desktop:sessions:remove")]
    public Task<RemoveDesktopSessionResult> RemoveSession(RemoveDesktopSessionRequest request)
        => desktopProjectionService.RemoveSessionAsync(request);

    [IpcInvoke("qwen-desktop:sessions:rename")]
    public Task<RenameDesktopSessionResult> RenameSession(RenameDesktopSessionRequest request)
        => desktopProjectionService.RenameSessionAsync(request);

    [IpcInvoke("qwen-desktop:sessions:get-active-turns")]
    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurns()
        => desktopProjectionService.GetActiveTurnsAsync();

    [IpcInvoke("qwen-desktop:arena:get-active-sessions")]
    public Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessions()
        => desktopProjectionService.GetActiveArenaSessionsAsync();

    [IpcInvoke("qwen-desktop:arena:cancel")]
    public Task<CancelArenaSessionResult> CancelArenaSession(CancelArenaSessionRequest request)
        => desktopProjectionService.CancelArenaSessionAsync(request);

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

    [IpcInvoke("qwen-desktop:channels:get-pairings")]
    public Task<ChannelPairingSnapshot> GetChannelPairings(GetChannelPairingRequest request)
        => desktopProjectionService.GetChannelPairingsAsync(request);

    [IpcInvoke("qwen-desktop:channels:approve-pairing")]
    public Task<ChannelPairingSnapshot> ApproveChannelPairing(ApproveChannelPairingRequest request)
        => desktopProjectionService.ApproveChannelPairingAsync(request);

    [IpcInvoke("qwen-desktop:workspace:get")]
    public Task<WorkspaceSnapshot> GetWorkspaceSnapshot()
        => desktopProjectionService.GetWorkspaceSnapshotAsync();

    [IpcInvoke("qwen-desktop:workspace:create-git-checkpoint")]
    public Task<WorkspaceSnapshot> CreateGitCheckpoint(CreateGitCheckpointRequest request)
        => desktopProjectionService.CreateGitCheckpointAsync(request);

    [IpcInvoke("qwen-desktop:workspace:restore-git-checkpoint")]
    public Task<WorkspaceSnapshot> RestoreGitCheckpoint(RestoreGitCheckpointRequest request)
        => desktopProjectionService.RestoreGitCheckpointAsync(request);

    [IpcInvoke("qwen-desktop:workspace:create-managed-worktree")]
    public Task<WorkspaceSnapshot> CreateManagedWorktree(CreateManagedWorktreeRequest request)
        => desktopProjectionService.CreateManagedWorktreeAsync(request);

    [IpcInvoke("qwen-desktop:workspace:cleanup-managed-session")]
    public Task<WorkspaceSnapshot> CleanupManagedSession(CleanupManagedWorktreeSessionRequest request)
        => desktopProjectionService.CleanupManagedSessionAsync(request);

    [IpcInvoke("qwen-desktop:mcp:add")]
    public Task<McpSnapshot> AddMcpServer(McpServerRegistrationRequest request)
        => desktopProjectionService.AddMcpServerAsync(request);

    [IpcInvoke("qwen-desktop:mcp:remove")]
    public Task<McpSnapshot> RemoveMcpServer(RemoveMcpServerRequest request)
        => desktopProjectionService.RemoveMcpServerAsync(request);

    [IpcInvoke("qwen-desktop:mcp:reconnect")]
    public Task<McpSnapshot> ReconnectMcpServer(ReconnectMcpServerRequest request)
        => desktopProjectionService.ReconnectMcpServerAsync(request);

    [IpcInvoke("qwen-desktop:prompts:get-registry")]
    public Task<PromptRegistrySnapshot> GetPromptRegistry(GetPromptRegistryRequest request)
        => desktopProjectionService.GetPromptRegistryAsync(request);

    [IpcInvoke("qwen-desktop:prompts:invoke")]
    public Task<McpPromptInvocationResult> InvokeRegisteredPrompt(InvokePromptRegistryEntryRequest request)
        => desktopProjectionService.InvokeRegisteredPromptAsync(request);

    [IpcInvoke("qwen-desktop:mcp-resources:get-registry")]
    public Task<McpResourceRegistrySnapshot> GetMcpResourceRegistry(GetMcpResourceRegistryRequest request)
        => desktopProjectionService.GetMcpResourceRegistryAsync(request);

    [IpcInvoke("qwen-desktop:mcp-resources:read")]
    public Task<McpResourceReadResult> ReadRegisteredMcpResource(ReadMcpResourceRegistryEntryRequest request)
        => desktopProjectionService.ReadRegisteredMcpResourceAsync(request);

    [IpcInvoke("qwen-desktop:extensions:get-settings")]
    public Task<ExtensionSettingsSnapshot> GetExtensionSettings(GetExtensionSettingsRequest request)
        => desktopProjectionService.GetExtensionSettingsAsync(request);

    [IpcInvoke("qwen-desktop:extensions:install")]
    public Task<ExtensionSnapshot> InstallExtension(InstallExtensionRequest request)
        => desktopProjectionService.InstallExtensionAsync(request);

    [IpcInvoke("qwen-desktop:extensions:preview-consent")]
    public Task<ExtensionConsentSnapshot> PreviewExtensionConsent(InstallExtensionRequest request)
        => desktopProjectionService.PreviewExtensionConsentAsync(request);

    [IpcInvoke("qwen-desktop:extensions:create-scaffold")]
    public Task<ExtensionScaffoldSnapshot> CreateExtensionScaffold(CreateExtensionScaffoldRequest request)
        => desktopProjectionService.CreateExtensionScaffoldAsync(request);

    [IpcInvoke("qwen-desktop:extensions:update")]
    public Task<ExtensionSnapshot> UpdateExtension(UpdateExtensionRequest request)
        => desktopProjectionService.UpdateExtensionAsync(request);

    [IpcInvoke("qwen-desktop:extensions:set-setting")]
    public Task<ExtensionSettingsSnapshot> SetExtensionSetting(SetExtensionSettingValueRequest request)
        => desktopProjectionService.SetExtensionSettingAsync(request);

    [IpcInvoke("qwen-desktop:extensions:set-enabled")]
    public Task<ExtensionSnapshot> SetExtensionEnabled(SetExtensionEnabledRequest request)
        => desktopProjectionService.SetExtensionEnabledAsync(request);

    [IpcInvoke("qwen-desktop:extensions:remove")]
    public Task<ExtensionSnapshot> RemoveExtension(RemoveExtensionRequest request)
        => desktopProjectionService.RemoveExtensionAsync(request);

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

    [IpcInvoke("qwen-desktop:direct-connect:create-session")]
    public Task<DirectConnectSessionState> CreateDirectConnectSession(CreateDirectConnectSessionRequest request)
        => directConnectSessionService.CreateSessionAsync(request);

    [IpcInvoke("qwen-desktop:direct-connect:get-server")]
    public Task<DirectConnectServerState> GetDirectConnectServer()
        => Task.FromResult(directConnectServerHost.State);

    [IpcInvoke("qwen-desktop:direct-connect:list-sessions")]
    public Task<IReadOnlyList<DirectConnectSessionState>> ListDirectConnectSessions()
        => directConnectSessionService.ListSessionsAsync();

    [IpcInvoke("qwen-desktop:direct-connect:get-session")]
    public Task<DirectConnectSessionState?> GetDirectConnectSession(GetDirectConnectSessionRequest request)
        => directConnectSessionService.GetSessionAsync(request.DirectConnectSessionId);

    [IpcInvoke("qwen-desktop:direct-connect:read-events")]
    public Task<DirectConnectSessionEventBatch> ReadDirectConnectSessionEvents(ReadDirectConnectSessionEventsRequest request)
        => directConnectSessionService.ReadEventsAsync(
            request.DirectConnectSessionId,
            request.AfterSequence,
            request.MaxCount);

    [IpcInvoke("qwen-desktop:direct-connect:start-turn")]
    public Task<DesktopSessionTurnResult> StartDirectConnectSessionTurn(StartDirectConnectSessionTurnRequest request)
        => directConnectSessionService.StartTurnAsync(request.DirectConnectSessionId, request.Turn);

    [IpcInvoke("qwen-desktop:direct-connect:approve-tool")]
    public Task<DesktopSessionTurnResult> ApproveDirectConnectSessionTool(ApproveDirectConnectSessionToolRequest request)
        => directConnectSessionService.ApprovePendingToolAsync(request.DirectConnectSessionId, request.Approval);

    [IpcInvoke("qwen-desktop:direct-connect:answer-question")]
    public Task<DesktopSessionTurnResult> AnswerDirectConnectSessionQuestion(AnswerDirectConnectSessionQuestionRequest request)
        => directConnectSessionService.AnswerPendingQuestionAsync(request.DirectConnectSessionId, request.Answer);

    [IpcInvoke("qwen-desktop:direct-connect:cancel-turn")]
    public Task<CancelDesktopSessionTurnResult> CancelDirectConnectSessionTurn(CancelDirectConnectSessionTurnRequest request)
        => directConnectSessionService.CancelTurnAsync(request.DirectConnectSessionId, request.Turn);

    [IpcInvoke("qwen-desktop:direct-connect:resume-interrupted")]
    public Task<DesktopSessionTurnResult> ResumeDirectConnectSessionTurn(ResumeDirectConnectSessionTurnRequest request)
        => directConnectSessionService.ResumeInterruptedTurnAsync(request.DirectConnectSessionId, request.Turn);

    [IpcInvoke("qwen-desktop:direct-connect:dismiss-interrupted")]
    public Task<DismissInterruptedTurnResult> DismissDirectConnectSessionTurn(DismissDirectConnectSessionTurnRequest request)
        => directConnectSessionService.DismissInterruptedTurnAsync(request.DirectConnectSessionId, request.Turn);

    [IpcInvoke("qwen-desktop:direct-connect:close-session")]
    public Task<DirectConnectSessionState> CloseDirectConnectSession(CloseDirectConnectSessionRequest request)
        => directConnectSessionService.CloseSessionAsync(request.DirectConnectSessionId);

    [IpcInvoke("qwen-desktop:workspace:select-project-directory")]
    public Task<SelectProjectDirectoryResult> SelectProjectDirectory()
        => desktopWindowBridge.SelectProjectDirectoryAsync();

    [IpcInvoke("qwen-desktop:followup:get-suggestions")]
    public Task<FollowupSuggestionSnapshot> GetFollowupSuggestions(GetFollowupSuggestionsRequest request)
        => desktopProjectionService.GetFollowupSuggestionsAsync(request);

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

    [IpcEvent("qwen-desktop:arena:event")]
    public void SubscribeArenaEvents(Action<ArenaSessionEvent> emit)
    {
        desktopProjectionService.ArenaEvent += (_, arenaEvent) => emit(arenaEvent);
    }
}
#pragma warning restore CS1591
