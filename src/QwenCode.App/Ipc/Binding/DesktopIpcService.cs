using QwenCode.App.Ipc.Attributes;
using QwenCode.App.Desktop;
using QwenCode.App.Models;

namespace QwenCode.App.Ipc;

/// <summary>
/// Represents the Desktop Ipc Service
/// </summary>
/// <param name="services">The services</param>
/// <param name="desktopProjectionService">The desktop projection service</param>
public sealed class DesktopIpcService(
    IServiceProvider services,
    IDesktopProjectionService desktopProjectionService) : IpcServiceBase(services)
{
    /// <summary>
    /// Executes bootstrap
    /// </summary>
    /// <returns>A task that resolves to app bootstrap payload</returns>
    [IpcInvoke("qwen-desktop:app:bootstrap")]
    public Task<AppBootstrapPayload> Bootstrap()
        => desktopProjectionService.GetBootstrapAsync();

    /// <summary>
    /// Gets session
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session detail?</returns>
    [IpcInvoke("qwen-desktop:sessions:get")]
    public Task<DesktopSessionDetail?> GetSession(GetDesktopSessionRequest request)
        => desktopProjectionService.GetSessionAsync(request);

    /// <summary>
    /// Removes session
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to remove desktop session result</returns>
    [IpcInvoke("qwen-desktop:sessions:remove")]
    public Task<RemoveDesktopSessionResult> RemoveSession(RemoveDesktopSessionRequest request)
        => desktopProjectionService.RemoveSessionAsync(request);

    /// <summary>
    /// Gets active turns
    /// </summary>
    /// <returns>A task that resolves to i read only list active turn state</returns>
    [IpcInvoke("qwen-desktop:sessions:get-active-turns")]
    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurns()
        => desktopProjectionService.GetActiveTurnsAsync();

    /// <summary>
    /// Gets active arena sessions
    /// </summary>
    /// <returns>A task that resolves to i read only list active arena session state</returns>
    [IpcInvoke("qwen-desktop:arena:get-active-sessions")]
    public Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessions()
        => desktopProjectionService.GetActiveArenaSessionsAsync();

    /// <summary>
    /// Cancels arena session
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel arena session result</returns>
    [IpcInvoke("qwen-desktop:arena:cancel")]
    public Task<CancelArenaSessionResult> CancelArenaSession(CancelArenaSessionRequest request)
        => desktopProjectionService.CancelArenaSessionAsync(request);

    /// <summary>
    /// Sets locale
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop state changed event</returns>
    [IpcInvoke("qwen-desktop:app:set-locale")]
    public Task<DesktopStateChangedEvent> SetLocale(SetLocaleRequest request)
        => desktopProjectionService.SetLocaleAsync(request.Locale);

    /// <summary>
    /// Gets auth status
    /// </summary>
    /// <returns>A task that resolves to auth status snapshot</returns>
    [IpcInvoke("qwen-desktop:auth:status")]
    public Task<AuthStatusSnapshot> GetAuthStatus()
        => desktopProjectionService.GetAuthStatusAsync();

    /// <summary>
    /// Executes configure open ai compatible auth
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    [IpcInvoke("qwen-desktop:auth:configure-openai-compatible")]
    public Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAuth(ConfigureOpenAiCompatibleAuthRequest request)
        => desktopProjectionService.ConfigureOpenAiCompatibleAuthAsync(request);

    /// <summary>
    /// Executes configure coding plan auth
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    [IpcInvoke("qwen-desktop:auth:configure-coding-plan")]
    public Task<AuthStatusSnapshot> ConfigureCodingPlanAuth(ConfigureCodingPlanAuthRequest request)
        => desktopProjectionService.ConfigureCodingPlanAuthAsync(request);

    /// <summary>
    /// Executes configure qwen o auth
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    [IpcInvoke("qwen-desktop:auth:configure-qwen-oauth")]
    public Task<AuthStatusSnapshot> ConfigureQwenOAuth(ConfigureQwenOAuthRequest request)
        => desktopProjectionService.ConfigureQwenOAuthAsync(request);

    /// <summary>
    /// Starts qwen o auth device flow
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    [IpcInvoke("qwen-desktop:auth:start-qwen-oauth-device-flow")]
    public Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlow(StartQwenOAuthDeviceFlowRequest request)
        => desktopProjectionService.StartQwenOAuthDeviceFlowAsync(request);

    /// <summary>
    /// Cancels qwen o auth device flow
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    [IpcInvoke("qwen-desktop:auth:cancel-qwen-oauth-device-flow")]
    public Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlow(CancelQwenOAuthDeviceFlowRequest request)
        => desktopProjectionService.CancelQwenOAuthDeviceFlowAsync(request);

    /// <summary>
    /// Disconnects auth
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    [IpcInvoke("qwen-desktop:auth:disconnect")]
    public Task<AuthStatusSnapshot> DisconnectAuth(DisconnectAuthRequest request)
        => desktopProjectionService.DisconnectAuthAsync(request);

    /// <summary>
    /// Gets channel pairings
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    [IpcInvoke("qwen-desktop:channels:get-pairings")]
    public Task<ChannelPairingSnapshot> GetChannelPairings(GetChannelPairingRequest request)
        => desktopProjectionService.GetChannelPairingsAsync(request);

    /// <summary>
    /// Approves channel pairing
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    [IpcInvoke("qwen-desktop:channels:approve-pairing")]
    public Task<ChannelPairingSnapshot> ApproveChannelPairing(ApproveChannelPairingRequest request)
        => desktopProjectionService.ApproveChannelPairingAsync(request);

    /// <summary>
    /// Gets workspace snapshot
    /// </summary>
    /// <returns>A task that resolves to workspace snapshot</returns>
    [IpcInvoke("qwen-desktop:workspace:get")]
    public Task<WorkspaceSnapshot> GetWorkspaceSnapshot()
        => desktopProjectionService.GetWorkspaceSnapshotAsync();

    /// <summary>
    /// Creates git checkpoint
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    [IpcInvoke("qwen-desktop:workspace:create-git-checkpoint")]
    public Task<WorkspaceSnapshot> CreateGitCheckpoint(CreateGitCheckpointRequest request)
        => desktopProjectionService.CreateGitCheckpointAsync(request);

    /// <summary>
    /// Restores git checkpoint
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    [IpcInvoke("qwen-desktop:workspace:restore-git-checkpoint")]
    public Task<WorkspaceSnapshot> RestoreGitCheckpoint(RestoreGitCheckpointRequest request)
        => desktopProjectionService.RestoreGitCheckpointAsync(request);

    /// <summary>
    /// Creates managed worktree
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    [IpcInvoke("qwen-desktop:workspace:create-managed-worktree")]
    public Task<WorkspaceSnapshot> CreateManagedWorktree(CreateManagedWorktreeRequest request)
        => desktopProjectionService.CreateManagedWorktreeAsync(request);

    /// <summary>
    /// Cleans up managed session
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    [IpcInvoke("qwen-desktop:workspace:cleanup-managed-session")]
    public Task<WorkspaceSnapshot> CleanupManagedSession(CleanupManagedWorktreeSessionRequest request)
        => desktopProjectionService.CleanupManagedSessionAsync(request);

    /// <summary>
    /// Executes add mcp server
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    [IpcInvoke("qwen-desktop:mcp:add")]
    public Task<McpSnapshot> AddMcpServer(McpServerRegistrationRequest request)
        => desktopProjectionService.AddMcpServerAsync(request);

    /// <summary>
    /// Removes mcp server
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    [IpcInvoke("qwen-desktop:mcp:remove")]
    public Task<McpSnapshot> RemoveMcpServer(RemoveMcpServerRequest request)
        => desktopProjectionService.RemoveMcpServerAsync(request);

    /// <summary>
    /// Executes reconnect mcp server
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    [IpcInvoke("qwen-desktop:mcp:reconnect")]
    public Task<McpSnapshot> ReconnectMcpServer(ReconnectMcpServerRequest request)
        => desktopProjectionService.ReconnectMcpServerAsync(request);

    /// <summary>
    /// Gets prompt registry
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to prompt registry snapshot</returns>
    [IpcInvoke("qwen-desktop:prompts:get-registry")]
    public Task<PromptRegistrySnapshot> GetPromptRegistry(GetPromptRegistryRequest request)
        => desktopProjectionService.GetPromptRegistryAsync(request);

    /// <summary>
    /// Invokes registered prompt
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp prompt invocation result</returns>
    [IpcInvoke("qwen-desktop:prompts:invoke")]
    public Task<McpPromptInvocationResult> InvokeRegisteredPrompt(InvokePromptRegistryEntryRequest request)
        => desktopProjectionService.InvokeRegisteredPromptAsync(request);

    /// <summary>
    /// Gets extension settings
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    [IpcInvoke("qwen-desktop:extensions:get-settings")]
    public Task<ExtensionSettingsSnapshot> GetExtensionSettings(GetExtensionSettingsRequest request)
        => desktopProjectionService.GetExtensionSettingsAsync(request);

    /// <summary>
    /// Executes install extension
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    [IpcInvoke("qwen-desktop:extensions:install")]
    public Task<ExtensionSnapshot> InstallExtension(InstallExtensionRequest request)
        => desktopProjectionService.InstallExtensionAsync(request);

    /// <summary>
    /// Executes preview extension consent
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension consent snapshot</returns>
    [IpcInvoke("qwen-desktop:extensions:preview-consent")]
    public Task<ExtensionConsentSnapshot> PreviewExtensionConsent(InstallExtensionRequest request)
        => desktopProjectionService.PreviewExtensionConsentAsync(request);

    /// <summary>
    /// Creates extension scaffold
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension scaffold snapshot</returns>
    [IpcInvoke("qwen-desktop:extensions:create-scaffold")]
    public Task<ExtensionScaffoldSnapshot> CreateExtensionScaffold(CreateExtensionScaffoldRequest request)
        => desktopProjectionService.CreateExtensionScaffoldAsync(request);

    /// <summary>
    /// Updates extension
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    [IpcInvoke("qwen-desktop:extensions:update")]
    public Task<ExtensionSnapshot> UpdateExtension(UpdateExtensionRequest request)
        => desktopProjectionService.UpdateExtensionAsync(request);

    /// <summary>
    /// Sets extension setting
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    [IpcInvoke("qwen-desktop:extensions:set-setting")]
    public Task<ExtensionSettingsSnapshot> SetExtensionSetting(SetExtensionSettingValueRequest request)
        => desktopProjectionService.SetExtensionSettingAsync(request);

    /// <summary>
    /// Sets extension enabled
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    [IpcInvoke("qwen-desktop:extensions:set-enabled")]
    public Task<ExtensionSnapshot> SetExtensionEnabled(SetExtensionEnabledRequest request)
        => desktopProjectionService.SetExtensionEnabledAsync(request);

    /// <summary>
    /// Removes extension
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    [IpcInvoke("qwen-desktop:extensions:remove")]
    public Task<ExtensionSnapshot> RemoveExtension(RemoveExtensionRequest request)
        => desktopProjectionService.RemoveExtensionAsync(request);

    /// <summary>
    /// Executes native tool
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    [IpcInvoke("qwen-desktop:tools:execute-native")]
    public Task<NativeToolExecutionResult> ExecuteNativeTool(ExecuteNativeToolRequest request)
        => desktopProjectionService.ExecuteNativeToolAsync(request);

    /// <summary>
    /// Starts session turn
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    [IpcInvoke("qwen-desktop:sessions:start-turn")]
    public Task<DesktopSessionTurnResult> StartSessionTurn(StartDesktopSessionTurnRequest request)
        => desktopProjectionService.StartSessionTurnAsync(request);

    /// <summary>
    /// Approves pending tool
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    [IpcInvoke("qwen-desktop:sessions:approve-tool")]
    public Task<DesktopSessionTurnResult> ApprovePendingTool(ApproveDesktopSessionToolRequest request)
        => desktopProjectionService.ApprovePendingToolAsync(request);

    /// <summary>
    /// Executes answer pending question
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    [IpcInvoke("qwen-desktop:sessions:answer-question")]
    public Task<DesktopSessionTurnResult> AnswerPendingQuestion(AnswerDesktopSessionQuestionRequest request)
        => desktopProjectionService.AnswerPendingQuestionAsync(request);

    /// <summary>
    /// Cancels session turn
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    [IpcInvoke("qwen-desktop:sessions:cancel-turn")]
    public Task<CancelDesktopSessionTurnResult> CancelSessionTurn(CancelDesktopSessionTurnRequest request)
        => desktopProjectionService.CancelSessionTurnAsync(request);

    /// <summary>
    /// Executes resume interrupted turn
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    [IpcInvoke("qwen-desktop:sessions:resume-interrupted")]
    public Task<DesktopSessionTurnResult> ResumeInterruptedTurn(ResumeInterruptedTurnRequest request)
        => desktopProjectionService.ResumeInterruptedTurnAsync(request);

    /// <summary>
    /// Executes dismiss interrupted turn
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    [IpcInvoke("qwen-desktop:sessions:dismiss-interrupted")]
    public Task<DismissInterruptedTurnResult> DismissInterruptedTurn(DismissInterruptedTurnRequest request)
        => desktopProjectionService.DismissInterruptedTurnAsync(request);

    /// <summary>
    /// Gets followup suggestions
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to followup suggestion snapshot</returns>
    [IpcInvoke("qwen-desktop:followup:get-suggestions")]
    public Task<FollowupSuggestionSnapshot> GetFollowupSuggestions(GetFollowupSuggestionsRequest request)
        => desktopProjectionService.GetFollowupSuggestionsAsync(request);

    /// <summary>
    /// Executes subscribe state changed
    /// </summary>
    /// <param name="emit">The emit</param>
    [IpcEvent("qwen-desktop:app:state-changed")]
    public void SubscribeStateChanged(Action<DesktopStateChangedEvent> emit)
    {
        desktopProjectionService.StateChanged += (_, state) => emit(state);
    }

    /// <summary>
    /// Executes subscribe auth changed
    /// </summary>
    /// <param name="emit">The emit</param>
    [IpcEvent("qwen-desktop:auth:changed")]
    public void SubscribeAuthChanged(Action<AuthStatusSnapshot> emit)
    {
        desktopProjectionService.AuthChanged += (_, state) => emit(state);
    }

    /// <summary>
    /// Executes subscribe session events
    /// </summary>
    /// <param name="emit">The emit</param>
    [IpcEvent("qwen-desktop:sessions:event")]
    public void SubscribeSessionEvents(Action<DesktopSessionEvent> emit)
    {
        desktopProjectionService.SessionEvent += (_, sessionEvent) => emit(sessionEvent);
    }

    /// <summary>
    /// Executes subscribe arena events
    /// </summary>
    /// <param name="emit">The emit</param>
    [IpcEvent("qwen-desktop:arena:event")]
    public void SubscribeArenaEvents(Action<ArenaSessionEvent> emit)
    {
        desktopProjectionService.ArenaEvent += (_, arenaEvent) => emit(arenaEvent);
    }
}
