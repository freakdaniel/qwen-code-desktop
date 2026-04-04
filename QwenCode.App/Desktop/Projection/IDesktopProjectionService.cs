using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopProjectionService
{
    event EventHandler<DesktopStateChangedEvent>? StateChanged;

    event EventHandler<DesktopSessionEvent>? SessionEvent;

    event EventHandler<AuthStatusSnapshot>? AuthChanged;

    event EventHandler<ArenaSessionEvent>? ArenaEvent;

    Task<AppBootstrapPayload> GetBootstrapAsync();

    Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync();

    Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync();

    Task<CancelArenaSessionResult> CancelArenaSessionAsync(CancelArenaSessionRequest request);

    Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request);

    Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request);

    Task<DesktopStateChangedEvent> SetLocaleAsync(string locale);

    Task<AuthStatusSnapshot> GetAuthStatusAsync();

    Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAuthAsync(ConfigureOpenAiCompatibleAuthRequest request);

    Task<AuthStatusSnapshot> ConfigureCodingPlanAuthAsync(ConfigureCodingPlanAuthRequest request);

    Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(ConfigureQwenOAuthRequest request);

    Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(StartQwenOAuthDeviceFlowRequest request);

    Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(CancelQwenOAuthDeviceFlowRequest request);

    Task<AuthStatusSnapshot> DisconnectAuthAsync(DisconnectAuthRequest request);

    Task<ChannelPairingSnapshot> GetChannelPairingsAsync(GetChannelPairingRequest request);

    Task<ChannelPairingSnapshot> ApproveChannelPairingAsync(ApproveChannelPairingRequest request);

    Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync();

    Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request);

    Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request);

    Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request);

    Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request);

    Task<McpSnapshot> AddMcpServerAsync(McpServerRegistrationRequest request);

    Task<McpSnapshot> RemoveMcpServerAsync(RemoveMcpServerRequest request);

    Task<McpSnapshot> ReconnectMcpServerAsync(ReconnectMcpServerRequest request);

    Task<PromptRegistrySnapshot> GetPromptRegistryAsync(GetPromptRegistryRequest request);

    Task<McpPromptInvocationResult> InvokeRegisteredPromptAsync(InvokePromptRegistryEntryRequest request);

    Task<ExtensionSettingsSnapshot> GetExtensionSettingsAsync(GetExtensionSettingsRequest request);

    Task<ExtensionSnapshot> InstallExtensionAsync(InstallExtensionRequest request);

    Task<ExtensionSettingsSnapshot> SetExtensionSettingAsync(SetExtensionSettingValueRequest request);

    Task<ExtensionSnapshot> SetExtensionEnabledAsync(SetExtensionEnabledRequest request);

    Task<ExtensionSnapshot> RemoveExtensionAsync(RemoveExtensionRequest request);

    Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request);

    Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request);

    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request);

    Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request);

    Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request);

    Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request);

    Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request);

    Task<FollowupSuggestionSnapshot> GetFollowupSuggestionsAsync(GetFollowupSuggestionsRequest request);
}
