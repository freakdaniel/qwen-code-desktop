using Microsoft.Extensions.Options;
using QwenCode.App.Agents;
using QwenCode.App.Auth;
using QwenCode.App.Channels;
using QwenCode.App.Compatibility;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Runtime;
using QwenCode.App.Sessions;
using QwenCode.App.Tools;
using QwenCode.App.Mcp;
using QwenCode.App.Extensions;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Bootstrap Projection Service
/// </summary>
/// <param name="options">The options</param>
/// <param name="workspacePathResolver">The workspace path resolver</param>
/// <param name="settingsResolver">The settings resolver</param>
/// <param name="projectSummaryService">The project summary service</param>
/// <param name="toolRegistry">The tool registry</param>
/// <param name="toolExecutor">The tool executor</param>
/// <param name="channelRegistryService">The channel registry service</param>
/// <param name="extensionCatalogService">The extension catalog service</param>
/// <param name="workspaceInspectionService">The workspace inspection service</param>
/// <param name="authFlowService">The auth flow service</param>
/// <param name="mcpConnectionManager">The mcp connection manager</param>
/// <param name="transcriptStore">The transcript store</param>
/// <param name="activeTurnRegistry">The active turn registry</param>
/// <param name="arenaSessionRegistry">The arena session registry</param>
/// <param name="interruptedTurnStore">The interrupted turn store</param>
public sealed class BootstrapProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    ISettingsResolver settingsResolver,
    IProjectSummaryService projectSummaryService,
    IToolRegistry toolRegistry,
    IToolExecutor toolExecutor,
    IChannelRegistryService channelRegistryService,
    IExtensionCatalogService extensionCatalogService,
    IWorkspaceInspectionService workspaceInspectionService,
    IAuthFlowService authFlowService,
    IMcpConnectionManager mcpConnectionManager,
    ITranscriptStore transcriptStore,
    IActiveTurnRegistry activeTurnRegistry,
    IArenaSessionRegistry arenaSessionRegistry,
    IInterruptedTurnStore interruptedTurnStore) : IDesktopBootstrapProjectionService
{
    private readonly DesktopShellOptions _options = options.Value;

    /// <summary>
    /// Creates bootstrap
    /// </summary>
    /// <param name="currentLocale">The current locale</param>
    /// <returns>The resulting app bootstrap payload</returns>
    public AppBootstrapPayload CreateBootstrap(string currentLocale)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        var runtime = settingsResolver.InspectRuntimeProfile(workspace);
        var projectSummary = projectSummaryService.Read(runtime) ?? CreateEmptyProjectSummary(workspace.WorkspaceRoot);

        return new AppBootstrapPayload
        {
            ProductName = _options.ProductName,
            CurrentMode = DesktopMode.Code,
            CurrentLocale = currentLocale,
            Locales = DesktopProjectionCatalog.SupportedLocales,
            WorkspaceRoot = workspace.WorkspaceRoot,
            Tracks = DesktopProjectionCatalog.Tracks,
            CompatibilityGoals = DesktopProjectionCatalog.CompatibilityGoals,
            CapabilityLanes = DesktopProjectionCatalog.CapabilityLanes,
            AdoptionPatterns = DesktopProjectionCatalog.AdoptionPatterns,
            RecentSessions = transcriptStore.ListSessions(workspace),
            ActiveTurns = activeTurnRegistry.ListActiveTurns(),
            ActiveArenaSessions = arenaSessionRegistry.ListActiveSessions(),
            RecoverableTurns = interruptedTurnStore.ListRecoverableTurns(runtime.ChatsDirectory),
            ProjectSummary = projectSummary,
            QwenCompatibility = settingsResolver.InspectCompatibility(workspace),
            QwenRuntime = runtime,
            QwenTools = toolRegistry.Inspect(workspace),
            QwenNativeHost = toolExecutor.Inspect(workspace),
            QwenChannels = channelRegistryService.Inspect(workspace),
            QwenExtensions = extensionCatalogService.Inspect(workspace),
            QwenWorkspace = workspaceInspectionService.Inspect(workspace),
            QwenAuth = authFlowService.GetStatus(workspace),
            QwenMcp = CreateMcpSnapshot(mcpConnectionManager.ListServersWithStatus(workspace))
        };
    }

    private static McpSnapshot CreateMcpSnapshot(IReadOnlyList<McpServerDefinition> servers) =>
        new()
        {
            TotalCount = servers.Count,
            ConnectedCount = servers.Count(static item => string.Equals(item.Status, "connected", StringComparison.OrdinalIgnoreCase)),
            DisconnectedCount = servers.Count(static item => string.Equals(item.Status, "disconnected", StringComparison.OrdinalIgnoreCase)),
            MissingCount = servers.Count(static item => string.Equals(item.Status, "missing", StringComparison.OrdinalIgnoreCase)),
            TokenCount = servers.Count(static item => item.HasPersistedToken),
            Servers = servers
        };

    private static ProjectSummarySnapshot CreateEmptyProjectSummary(string workspaceRoot) =>
        new()
        {
            HasHistory = false,
            FilePath = Path.Combine(Path.GetFullPath(workspaceRoot), ".qwen", "PROJECT_SUMMARY.md"),
            Content = string.Empty,
            TimestampText = string.Empty,
            TimeAgo = string.Empty,
            OverallGoal = string.Empty,
            CurrentPlan = string.Empty,
            TotalTasks = 0,
            DoneCount = 0,
            InProgressCount = 0,
            TodoCount = 0,
            PendingTasks = [],
            TimestampUtc = DateTime.MinValue
        };
}
