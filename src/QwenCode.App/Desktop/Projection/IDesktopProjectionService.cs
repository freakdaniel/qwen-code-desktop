using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Projection Service
/// </summary>
public interface IDesktopProjectionService
{
    /// <summary>
    /// Occurs when State Changed
    /// </summary>
    event EventHandler<DesktopStateChangedEvent>? StateChanged;

    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    event EventHandler<DesktopSessionEvent>? SessionEvent;

    /// <summary>
    /// Occurs when Auth Changed
    /// </summary>
    event EventHandler<AuthStatusSnapshot>? AuthChanged;

    /// <summary>
    /// Occurs when Arena Event
    /// </summary>
    event EventHandler<ArenaSessionEvent>? ArenaEvent;

    /// <summary>
    /// Gets bootstrap async
    /// </summary>
    /// <returns>A task that resolves to app bootstrap payload</returns>
    Task<AppBootstrapPayload> GetBootstrapAsync();

    /// <summary>
    /// Gets active turns async
    /// </summary>
    /// <returns>A task that resolves to i read only list active turn state</returns>
    Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync();

    /// <summary>
    /// Gets active arena sessions async
    /// </summary>
    /// <returns>A task that resolves to i read only list active arena session state</returns>
    Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync();

    /// <summary>
    /// Cancels arena session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel arena session result</returns>
    Task<CancelArenaSessionResult> CancelArenaSessionAsync(CancelArenaSessionRequest request);

    /// <summary>
    /// Gets session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session detail?</returns>
    Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request);

    /// <summary>
    /// Removes session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to remove desktop session result</returns>
    Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request);

    /// <summary>
    /// Sets locale async
    /// </summary>
    /// <param name="locale">The locale</param>
    /// <returns>A task that resolves to desktop state changed event</returns>
    Task<DesktopStateChangedEvent> SetLocaleAsync(string locale);

    /// <summary>
    /// Gets auth status async
    /// </summary>
    /// <returns>A task that resolves to auth status snapshot</returns>
    Task<AuthStatusSnapshot> GetAuthStatusAsync();

    /// <summary>
    /// Executes configure open ai compatible auth async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAuthAsync(ConfigureOpenAiCompatibleAuthRequest request);

    /// <summary>
    /// Executes configure coding plan auth async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    Task<AuthStatusSnapshot> ConfigureCodingPlanAuthAsync(ConfigureCodingPlanAuthRequest request);

    /// <summary>
    /// Executes configure qwen o auth async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(ConfigureQwenOAuthRequest request);

    /// <summary>
    /// Starts qwen o auth device flow async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(StartQwenOAuthDeviceFlowRequest request);

    /// <summary>
    /// Cancels qwen o auth device flow async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(CancelQwenOAuthDeviceFlowRequest request);

    /// <summary>
    /// Disconnects auth async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    Task<AuthStatusSnapshot> DisconnectAuthAsync(DisconnectAuthRequest request);

    /// <summary>
    /// Gets channel pairings async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    Task<ChannelPairingSnapshot> GetChannelPairingsAsync(GetChannelPairingRequest request);

    /// <summary>
    /// Approves channel pairing async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    Task<ChannelPairingSnapshot> ApproveChannelPairingAsync(ApproveChannelPairingRequest request);

    /// <summary>
    /// Gets workspace snapshot async
    /// </summary>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync();

    /// <summary>
    /// Creates git checkpoint async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request);

    /// <summary>
    /// Restores git checkpoint async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request);

    /// <summary>
    /// Creates managed worktree async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request);

    /// <summary>
    /// Cleans up managed session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request);

    /// <summary>
    /// Executes add mcp server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    Task<McpSnapshot> AddMcpServerAsync(McpServerRegistrationRequest request);

    /// <summary>
    /// Removes mcp server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    Task<McpSnapshot> RemoveMcpServerAsync(RemoveMcpServerRequest request);

    /// <summary>
    /// Executes reconnect mcp server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    Task<McpSnapshot> ReconnectMcpServerAsync(ReconnectMcpServerRequest request);

    /// <summary>
    /// Gets prompt registry async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to prompt registry snapshot</returns>
    Task<PromptRegistrySnapshot> GetPromptRegistryAsync(GetPromptRegistryRequest request);

    /// <summary>
    /// Invokes registered prompt async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp prompt invocation result</returns>
    Task<McpPromptInvocationResult> InvokeRegisteredPromptAsync(InvokePromptRegistryEntryRequest request);

    /// <summary>
    /// Gets extension settings async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    Task<ExtensionSettingsSnapshot> GetExtensionSettingsAsync(GetExtensionSettingsRequest request);

    /// <summary>
    /// Executes install extension async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    Task<ExtensionSnapshot> InstallExtensionAsync(InstallExtensionRequest request);

    /// <summary>
    /// Executes preview extension consent async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension consent snapshot</returns>
    Task<ExtensionConsentSnapshot> PreviewExtensionConsentAsync(InstallExtensionRequest request);

    /// <summary>
    /// Creates extension scaffold async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension scaffold snapshot</returns>
    Task<ExtensionScaffoldSnapshot> CreateExtensionScaffoldAsync(CreateExtensionScaffoldRequest request);

    /// <summary>
    /// Updates extension async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    Task<ExtensionSnapshot> UpdateExtensionAsync(UpdateExtensionRequest request);

    /// <summary>
    /// Sets extension setting async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    Task<ExtensionSettingsSnapshot> SetExtensionSettingAsync(SetExtensionSettingValueRequest request);

    /// <summary>
    /// Sets extension enabled async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    Task<ExtensionSnapshot> SetExtensionEnabledAsync(SetExtensionEnabledRequest request);

    /// <summary>
    /// Removes extension async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    Task<ExtensionSnapshot> RemoveExtensionAsync(RemoveExtensionRequest request);

    /// <summary>
    /// Executes native tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request);

    /// <summary>
    /// Starts session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request);

    /// <summary>
    /// Approves pending tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request);

    /// <summary>
    /// Executes answer pending question async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request);

    /// <summary>
    /// Cancels session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request);

    /// <summary>
    /// Executes resume interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request);

    /// <summary>
    /// Executes dismiss interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request);

    /// <summary>
    /// Gets followup suggestions async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to followup suggestion snapshot</returns>
    Task<FollowupSuggestionSnapshot> GetFollowupSuggestionsAsync(GetFollowupSuggestionsRequest request);
}
