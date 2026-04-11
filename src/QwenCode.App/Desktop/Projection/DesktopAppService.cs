using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Desktop App Service
/// </summary>
/// <param name="localeStateService">The locale state service</param>
/// <param name="bootstrapProjectionService">The bootstrap projection service</param>
/// <param name="arenaProjectionService">The arena projection service</param>
/// <param name="authProjectionService">The auth projection service</param>
/// <param name="channelProjectionService">The channel projection service</param>
/// <param name="workspaceProjectionService">The workspace projection service</param>
/// <param name="mcpProjectionService">The mcp projection service</param>
/// <param name="promptProjectionService">The prompt projection service</param>
/// <param name="mcpResourceProjectionService">The mcp resource projection service</param>
/// <param name="followupProjectionService">The followup projection service</param>
/// <param name="extensionProjectionService">The extension projection service</param>
/// <param name="sessionProjectionService">The session projection service</param>
public sealed class DesktopAppService(
    ILocaleStateService localeStateService,
    IDesktopBootstrapProjectionService bootstrapProjectionService,
    IDesktopArenaProjectionService arenaProjectionService,
    IDesktopAuthProjectionService authProjectionService,
    IDesktopChannelProjectionService channelProjectionService,
    IDesktopWorkspaceProjectionService workspaceProjectionService,
    IDesktopMcpProjectionService mcpProjectionService,
    IDesktopPromptProjectionService promptProjectionService,
    IDesktopMcpResourceProjectionService mcpResourceProjectionService,
    IDesktopFollowupProjectionService followupProjectionService,
    IDesktopExtensionProjectionService extensionProjectionService,
    IDesktopSessionProjectionService sessionProjectionService) : IDesktopProjectionService
{
    /// <summary>
    /// Occurs when State Changed
    /// </summary>
    public event EventHandler<DesktopStateChangedEvent>? StateChanged;

    /// <summary>
    /// Occurs when Auth Changed
    /// </summary>
    public event EventHandler<AuthStatusSnapshot>? AuthChanged
    {
        add => authProjectionService.AuthChanged += value;
        remove => authProjectionService.AuthChanged -= value;
    }

    /// <summary>
    /// Occurs when Arena Event
    /// </summary>
    public event EventHandler<ArenaSessionEvent>? ArenaEvent
    {
        add => arenaProjectionService.ArenaEvent += value;
        remove => arenaProjectionService.ArenaEvent -= value;
    }

    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    public event EventHandler<DesktopSessionEvent>? SessionEvent
    {
        add => sessionProjectionService.SessionEvent += value;
        remove => sessionProjectionService.SessionEvent -= value;
    }

    /// <summary>
    /// Gets bootstrap async
    /// </summary>
    /// <returns>A task that resolves to app bootstrap payload</returns>
    public Task<AppBootstrapPayload> GetBootstrapAsync() =>
        Task.FromResult(bootstrapProjectionService.CreateBootstrap(localeStateService.CurrentLocale));

    /// <summary>
    /// Gets active arena sessions async
    /// </summary>
    /// <returns>A task that resolves to i read only list active arena session state</returns>
    public Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync() =>
        arenaProjectionService.GetActiveArenaSessionsAsync();

    /// <summary>
    /// Cancels arena session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel arena session result</returns>
    public Task<CancelArenaSessionResult> CancelArenaSessionAsync(CancelArenaSessionRequest request) =>
        arenaProjectionService.CancelArenaSessionAsync(request);

    /// <summary>
    /// Sets locale async
    /// </summary>
    /// <param name="locale">The locale</param>
    /// <returns>A task that resolves to desktop state changed event</returns>
    public Task<DesktopStateChangedEvent> SetLocaleAsync(string locale)
    {
        var state = localeStateService.SetLocale(locale);
        StateChanged?.Invoke(this, state);
        return Task.FromResult(state);
    }

    /// <summary>
    /// Gets auth status async
    /// </summary>
    /// <returns>A task that resolves to auth status snapshot</returns>
    public Task<AuthStatusSnapshot> GetAuthStatusAsync() =>
        Task.FromResult(authProjectionService.CreateSnapshot());

    /// <summary>
    /// Executes configure open ai compatible auth async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    public Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAuthAsync(ConfigureOpenAiCompatibleAuthRequest request) =>
        authProjectionService.ConfigureOpenAiCompatibleAsync(request);

    /// <summary>
    /// Executes configure coding plan auth async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    public Task<AuthStatusSnapshot> ConfigureCodingPlanAuthAsync(ConfigureCodingPlanAuthRequest request) =>
        authProjectionService.ConfigureCodingPlanAsync(request);

    /// <summary>
    /// Executes configure qwen o auth async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    public Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(ConfigureQwenOAuthRequest request) =>
        authProjectionService.ConfigureQwenOAuthAsync(request);

    /// <summary>
    /// Starts qwen o auth device flow async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    public Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(StartQwenOAuthDeviceFlowRequest request) =>
        authProjectionService.StartQwenOAuthDeviceFlowAsync(request);

    /// <summary>
    /// Cancels qwen o auth device flow async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    public Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(CancelQwenOAuthDeviceFlowRequest request) =>
        authProjectionService.CancelQwenOAuthDeviceFlowAsync(request);

    /// <summary>
    /// Disconnects auth async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to auth status snapshot</returns>
    public Task<AuthStatusSnapshot> DisconnectAuthAsync(DisconnectAuthRequest request) =>
        authProjectionService.DisconnectAsync(request);

    /// <summary>
    /// Gets channel pairings async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    public Task<ChannelPairingSnapshot> GetChannelPairingsAsync(GetChannelPairingRequest request) =>
        channelProjectionService.GetPairingsAsync(request);

    /// <summary>
    /// Approves channel pairing async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    public Task<ChannelPairingSnapshot> ApproveChannelPairingAsync(ApproveChannelPairingRequest request) =>
        channelProjectionService.ApprovePairingAsync(request);

    /// <summary>
    /// Gets workspace snapshot async
    /// </summary>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync() =>
        workspaceProjectionService.GetSnapshotAsync();

    /// <summary>
    /// Creates git checkpoint async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request) =>
        workspaceProjectionService.CreateGitCheckpointAsync(request);

    /// <summary>
    /// Restores git checkpoint async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request) =>
        workspaceProjectionService.RestoreGitCheckpointAsync(request);

    /// <summary>
    /// Creates managed worktree async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request) =>
        workspaceProjectionService.CreateManagedWorktreeAsync(request);

    /// <summary>
    /// Cleans up managed session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to workspace snapshot</returns>
    public Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request) =>
        workspaceProjectionService.CleanupManagedSessionAsync(request);

    /// <summary>
    /// Executes add mcp server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    public Task<McpSnapshot> AddMcpServerAsync(McpServerRegistrationRequest request) =>
        mcpProjectionService.AddServerAsync(request);

    /// <summary>
    /// Removes mcp server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    public Task<McpSnapshot> RemoveMcpServerAsync(RemoveMcpServerRequest request) =>
        mcpProjectionService.RemoveServerAsync(request);

    /// <summary>
    /// Executes reconnect mcp server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    public Task<McpSnapshot> ReconnectMcpServerAsync(ReconnectMcpServerRequest request) =>
        mcpProjectionService.ReconnectServerAsync(request);

    /// <summary>
    /// Gets prompt registry async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to prompt registry snapshot</returns>
    public Task<PromptRegistrySnapshot> GetPromptRegistryAsync(GetPromptRegistryRequest request) =>
        promptProjectionService.GetPromptRegistryAsync(request);

    /// <summary>
    /// Invokes registered prompt async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp prompt invocation result</returns>
    public Task<McpPromptInvocationResult> InvokeRegisteredPromptAsync(InvokePromptRegistryEntryRequest request) =>
        promptProjectionService.InvokeRegisteredPromptAsync(request);

    /// <summary>
    /// Gets MCP resource registry async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to MCP resource registry snapshot</returns>
    public Task<McpResourceRegistrySnapshot> GetMcpResourceRegistryAsync(GetMcpResourceRegistryRequest request) =>
        mcpResourceProjectionService.GetResourceRegistryAsync(request);

    /// <summary>
    /// Reads registered MCP resource async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to MCP resource read result</returns>
    public Task<McpResourceReadResult> ReadRegisteredMcpResourceAsync(ReadMcpResourceRegistryEntryRequest request) =>
        mcpResourceProjectionService.ReadRegisteredResourceAsync(request);

    /// <summary>
    /// Gets extension settings async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    public Task<ExtensionSettingsSnapshot> GetExtensionSettingsAsync(GetExtensionSettingsRequest request) =>
        extensionProjectionService.GetSettingsAsync(request);

    /// <summary>
    /// Executes install extension async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    public Task<ExtensionSnapshot> InstallExtensionAsync(InstallExtensionRequest request) =>
        extensionProjectionService.InstallAsync(request);

    /// <summary>
    /// Executes preview extension consent async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension consent snapshot</returns>
    public Task<ExtensionConsentSnapshot> PreviewExtensionConsentAsync(InstallExtensionRequest request) =>
        extensionProjectionService.PreviewConsentAsync(request);

    /// <summary>
    /// Creates extension scaffold async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension scaffold snapshot</returns>
    public Task<ExtensionScaffoldSnapshot> CreateExtensionScaffoldAsync(CreateExtensionScaffoldRequest request) =>
        extensionProjectionService.CreateScaffoldAsync(request);

    /// <summary>
    /// Updates extension async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    public Task<ExtensionSnapshot> UpdateExtensionAsync(UpdateExtensionRequest request) =>
        extensionProjectionService.UpdateAsync(request);

    /// <summary>
    /// Sets extension setting async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    public Task<ExtensionSettingsSnapshot> SetExtensionSettingAsync(SetExtensionSettingValueRequest request) =>
        extensionProjectionService.SetSettingAsync(request);

    /// <summary>
    /// Sets extension enabled async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    public Task<ExtensionSnapshot> SetExtensionEnabledAsync(SetExtensionEnabledRequest request) =>
        extensionProjectionService.SetEnabledAsync(request);

    /// <summary>
    /// Removes extension async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    public Task<ExtensionSnapshot> RemoveExtensionAsync(RemoveExtensionRequest request) =>
        extensionProjectionService.RemoveAsync(request);

    /// <summary>
    /// Gets session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session detail?</returns>
    public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) =>
        sessionProjectionService.GetSessionAsync(request);

    /// <summary>
    /// Removes session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to remove desktop session result</returns>
    public Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request) =>
        sessionProjectionService.RemoveSessionAsync(request);

    /// <summary>
    /// Gets active turns async
    /// </summary>
    /// <returns>A task that resolves to i read only list active turn state</returns>
    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync() =>
        sessionProjectionService.GetActiveTurnsAsync();

    /// <summary>
    /// Executes native tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    public Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) =>
        sessionProjectionService.ExecuteNativeToolAsync(request);

    /// <summary>
    /// Starts session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request) =>
        sessionProjectionService.StartSessionTurnAsync(request);

    /// <summary>
    /// Approves pending tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request) =>
        sessionProjectionService.ApprovePendingToolAsync(request);

    /// <summary>
    /// Executes answer pending question async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request) =>
        sessionProjectionService.AnswerPendingQuestionAsync(request);

    /// <summary>
    /// Cancels session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) =>
        sessionProjectionService.CancelSessionTurnAsync(request);

    /// <summary>
    /// Executes resume interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) =>
        sessionProjectionService.ResumeInterruptedTurnAsync(request);

    /// <summary>
    /// Executes dismiss interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) =>
        sessionProjectionService.DismissInterruptedTurnAsync(request);

    /// <summary>
    /// Gets followup suggestions async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to followup suggestion snapshot</returns>
    public Task<FollowupSuggestionSnapshot> GetFollowupSuggestionsAsync(GetFollowupSuggestionsRequest request) =>
        followupProjectionService.GetSuggestionsAsync(request);
}
