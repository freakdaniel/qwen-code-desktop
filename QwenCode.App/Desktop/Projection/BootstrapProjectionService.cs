using Microsoft.Extensions.Options;
using QwenCode.App.Auth;
using QwenCode.App.Compatibility;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Runtime;
using QwenCode.App.Sessions;
using QwenCode.App.Tools;
using QwenCode.App.Mcp;

namespace QwenCode.App.Desktop;

public sealed class BootstrapProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    ISettingsResolver settingsResolver,
    IProjectSummaryService projectSummaryService,
    IToolRegistry toolRegistry,
    IToolExecutor toolExecutor,
    IAuthFlowService authFlowService,
    IMcpConnectionManager mcpConnectionManager,
    ITranscriptStore transcriptStore,
    IActiveTurnRegistry activeTurnRegistry,
    IInterruptedTurnStore interruptedTurnStore) : IDesktopBootstrapProjectionService
{
    private readonly DesktopShellOptions _options = options.Value;

    public AppBootstrapPayload CreateBootstrap(string currentLocale)
    {
        var workspace = workspacePathResolver.Resolve(_options.Workspace);
        var runtime = settingsResolver.InspectRuntimeProfile(workspace);
        var projectSummary = projectSummaryService.Read(workspace.WorkspaceRoot) ?? CreateEmptyProjectSummary(workspace.WorkspaceRoot);

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
            RecoverableTurns = interruptedTurnStore.ListRecoverableTurns(runtime.ChatsDirectory),
            ProjectSummary = projectSummary,
            QwenCompatibility = settingsResolver.InspectCompatibility(workspace),
            QwenRuntime = runtime,
            QwenTools = toolRegistry.Inspect(workspace),
            QwenNativeHost = toolExecutor.Inspect(workspace),
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
