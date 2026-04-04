using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public sealed class DesktopAppService(
    ILocaleStateService localeStateService,
    IDesktopBootstrapProjectionService bootstrapProjectionService,
    IDesktopArenaProjectionService arenaProjectionService,
    IDesktopAuthProjectionService authProjectionService,
    IDesktopChannelProjectionService channelProjectionService,
    IDesktopWorkspaceProjectionService workspaceProjectionService,
    IDesktopMcpProjectionService mcpProjectionService,
    IDesktopPromptProjectionService promptProjectionService,
    IDesktopFollowupProjectionService followupProjectionService,
    IDesktopExtensionProjectionService extensionProjectionService,
    IDesktopSessionProjectionService sessionProjectionService) : IDesktopProjectionService
{
    public event EventHandler<DesktopStateChangedEvent>? StateChanged;

    public event EventHandler<AuthStatusSnapshot>? AuthChanged
    {
        add => authProjectionService.AuthChanged += value;
        remove => authProjectionService.AuthChanged -= value;
    }

    public event EventHandler<ArenaSessionEvent>? ArenaEvent
    {
        add => arenaProjectionService.ArenaEvent += value;
        remove => arenaProjectionService.ArenaEvent -= value;
    }

    public event EventHandler<DesktopSessionEvent>? SessionEvent
    {
        add => sessionProjectionService.SessionEvent += value;
        remove => sessionProjectionService.SessionEvent -= value;
    }

    public Task<AppBootstrapPayload> GetBootstrapAsync() =>
        Task.FromResult(bootstrapProjectionService.CreateBootstrap(localeStateService.CurrentLocale));

    public Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync() =>
        arenaProjectionService.GetActiveArenaSessionsAsync();

    public Task<CancelArenaSessionResult> CancelArenaSessionAsync(CancelArenaSessionRequest request) =>
        arenaProjectionService.CancelArenaSessionAsync(request);

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

    public Task<ChannelPairingSnapshot> GetChannelPairingsAsync(GetChannelPairingRequest request) =>
        channelProjectionService.GetPairingsAsync(request);

    public Task<ChannelPairingSnapshot> ApproveChannelPairingAsync(ApproveChannelPairingRequest request) =>
        channelProjectionService.ApprovePairingAsync(request);

    public Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync() =>
        workspaceProjectionService.GetSnapshotAsync();

    public Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request) =>
        workspaceProjectionService.CreateGitCheckpointAsync(request);

    public Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request) =>
        workspaceProjectionService.RestoreGitCheckpointAsync(request);

    public Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request) =>
        workspaceProjectionService.CreateManagedWorktreeAsync(request);

    public Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request) =>
        workspaceProjectionService.CleanupManagedSessionAsync(request);

    public Task<McpSnapshot> AddMcpServerAsync(McpServerRegistrationRequest request) =>
        mcpProjectionService.AddServerAsync(request);

    public Task<McpSnapshot> RemoveMcpServerAsync(RemoveMcpServerRequest request) =>
        mcpProjectionService.RemoveServerAsync(request);

    public Task<McpSnapshot> ReconnectMcpServerAsync(ReconnectMcpServerRequest request) =>
        mcpProjectionService.ReconnectServerAsync(request);

    public Task<PromptRegistrySnapshot> GetPromptRegistryAsync(GetPromptRegistryRequest request) =>
        promptProjectionService.GetPromptRegistryAsync(request);

    public Task<McpPromptInvocationResult> InvokeRegisteredPromptAsync(InvokePromptRegistryEntryRequest request) =>
        promptProjectionService.InvokeRegisteredPromptAsync(request);

    public Task<ExtensionSettingsSnapshot> GetExtensionSettingsAsync(GetExtensionSettingsRequest request) =>
        extensionProjectionService.GetSettingsAsync(request);

    public Task<ExtensionSnapshot> InstallExtensionAsync(InstallExtensionRequest request) =>
        extensionProjectionService.InstallAsync(request);

    public Task<ExtensionConsentSnapshot> PreviewExtensionConsentAsync(InstallExtensionRequest request) =>
        extensionProjectionService.PreviewConsentAsync(request);

    public Task<ExtensionScaffoldSnapshot> CreateExtensionScaffoldAsync(CreateExtensionScaffoldRequest request) =>
        extensionProjectionService.CreateScaffoldAsync(request);

    public Task<ExtensionSnapshot> UpdateExtensionAsync(UpdateExtensionRequest request) =>
        extensionProjectionService.UpdateAsync(request);

    public Task<ExtensionSettingsSnapshot> SetExtensionSettingAsync(SetExtensionSettingValueRequest request) =>
        extensionProjectionService.SetSettingAsync(request);

    public Task<ExtensionSnapshot> SetExtensionEnabledAsync(SetExtensionEnabledRequest request) =>
        extensionProjectionService.SetEnabledAsync(request);

    public Task<ExtensionSnapshot> RemoveExtensionAsync(RemoveExtensionRequest request) =>
        extensionProjectionService.RemoveAsync(request);

    public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) =>
        sessionProjectionService.GetSessionAsync(request);

    public Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request) =>
        sessionProjectionService.RemoveSessionAsync(request);

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

    public Task<FollowupSuggestionSnapshot> GetFollowupSuggestionsAsync(GetFollowupSuggestionsRequest request) =>
        followupProjectionService.GetSuggestionsAsync(request);
}
