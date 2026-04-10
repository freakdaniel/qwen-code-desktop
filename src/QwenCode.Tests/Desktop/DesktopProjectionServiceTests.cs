using QwenCode.Core.Auth;
using QwenCode.Core.Channels;
using QwenCode.Core.Config;
using QwenCode.App.Desktop;
using QwenCode.App.Desktop.Projection;
using QwenCode.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.Tests.Shared.Fakes;

namespace QwenCode.Tests.Desktop;

public sealed class DesktopProjectionServiceTests
{
    [Fact]
    public async Task SetLocaleAsync_FallsBackToKnownLanguageCode()
    {
        var service = CreateService();

        var state = await service.SetLocaleAsync("fr-CA");

        Assert.Equal("fr", state.CurrentLocale);
    }

    [Fact]
    public async Task GetBootstrapAsync_ReturnsConfiguredProductAndWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-bootstrap-{Guid.NewGuid():N}");
        var expectedWorkspace = new WorkspacePaths
        {
            WorkspaceRoot = Path.Combine(root, "workspace")
        };

        Directory.CreateDirectory(expectedWorkspace.WorkspaceRoot);
        Directory.CreateDirectory(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "AppHost"));
        Directory.CreateDirectory(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Runtime"));
        Directory.CreateDirectory(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Sessions"));
        Directory.CreateDirectory(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Ipc"));
        Directory.CreateDirectory(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "electron"));

        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "QwenCode.App.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Program.cs"), "internal static class Program { }");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "AppHost", "Bootstrapper.cs"), "internal sealed class Bootstrapper { }");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "electron", "preload.js"), "module.exports = {};");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Runtime", "CommandActionRuntime.cs"), "internal sealed class CommandActionRuntime { }");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Runtime", "SlashCommandRuntime.cs"), "internal sealed class SlashCommandRuntime { }");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Sessions", "DesktopSessionHostService.cs"), "internal sealed class DesktopSessionHostService { }");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Sessions", "DesktopSessionCatalogService.cs"), "internal sealed class DesktopSessionCatalogService { }");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Ipc", "DesktopIpcService.cs"), "internal sealed class DesktopIpcService { }");
        File.WriteAllText(Path.Combine(expectedWorkspace.WorkspaceRoot, "QwenCode.App", "Ipc", "IpcServiceBase.cs"), "internal abstract class IpcServiceBase { }");

        try
        {
            var service = CreateService(new DesktopShellOptions
            {
                ProductName = "Qwen Code Desktop",
                DefaultLocale = "ru",
                Workspace = expectedWorkspace
            });

            var payload = await service.GetBootstrapAsync();

            Assert.Equal("Qwen Code Desktop", payload.ProductName);
            Assert.Equal(DesktopMode.Code, payload.CurrentMode);
            Assert.Equal(expectedWorkspace.WorkspaceRoot, payload.WorkspaceRoot);
            Assert.Contains(payload.Locales, locale => locale.Code == "ar");
            Assert.Contains(payload.QwenCompatibility.SettingsLayers, layer => layer.Id == "project-settings");
            Assert.False(string.IsNullOrWhiteSpace(payload.QwenRuntime.ApprovalProfile.DefaultMode));
            Assert.True(payload.QwenTools.TotalCount >= 0);
            Assert.True(payload.QwenNativeHost.RegisteredCount >= 0);
            Assert.False(string.IsNullOrWhiteSpace(payload.QwenAuth.SelectedType));
            Assert.True(payload.QwenWorkspace.Discovery.VisibleFileCount >= 0);
            Assert.True(payload.QwenWorkspace.Git.ManagedSessionCount >= 0);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetBootstrapAsync_IncludesRecoverableTurnsFromInterruptedStore()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-bootstrap-recovery-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var approvalPolicyService = new ApprovalPolicyService();
            var workspacePathResolver = new WorkspacePathResolver(environmentPaths);
            var projectSummaryService = new ProjectSummaryService();
            var shellOptions = Microsoft.Extensions.Options.Options.Create(new DesktopShellOptions
            {
                Workspace = new WorkspacePaths
                {
                    WorkspaceRoot = workspaceRoot
                }
            });
            var settingsResolver = new DesktopSettingsResolver(
                compatibilityService,
                runtimeProfileService);
            var gitHistoryService = new GitHistoryService(new GitCliService(), runtimeProfileService);
            var authFlowService = new AuthFlowService(
                runtimeProfileService,
                environmentPaths,
                new FileQwenOAuthCredentialStore(environmentPaths),
                new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))),
                new FakeAuthUrlLauncher(),
                new QwenOAuthTokenManager(
                    new FileQwenOAuthCredentialStore(environmentPaths),
                    environmentPaths,
                    new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));
            var toolRegistry = new ToolCatalogService(runtimeProfileService, approvalPolicyService);
            var toolExecutor = new NativeToolHostService(runtimeProfileService, approvalPolicyService);
            var workspaceInspectionService = new WorkspaceInspectionService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new FileDiscoveryService(new GitCliService(), runtimeProfileService));
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var channelRegistry = new ChannelRegistryService(
                environmentPaths,
                settingsResolver,
                extensionCatalog);
            var mcpTokenStore = new FileMcpTokenStore(environmentPaths);
            var mcpRegistry = new McpRegistryService(runtimeProfileService, mcpTokenStore);
            var mcpConnectionManager = new McpConnectionManagerService(
                mcpRegistry,
                new McpToolRuntimeService(mcpRegistry, mcpTokenStore, new HttpClient(), runtimeProfileService));
            var promptRegistryService = new PromptRegistryService(
                mcpConnectionManager,
                new McpToolRuntimeService(mcpRegistry, mcpTokenStore, new HttpClient(), runtimeProfileService));
            var modelRegistry = new ModelRegistryService(
                new RuntimeConfigService(environmentPaths),
                new TokenLimitService(),
                Microsoft.Extensions.Options.Options.Create(new NativeAssistantRuntimeOptions()));
            var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessionService = (ISessionService)transcriptStore;
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

            var interruptedStore = new InterruptedTurnStore();
            var activeTurnRegistry = new ActiveTurnRegistry(interruptedStore);
            var arenaSessionRegistry = new ArenaSessionRegistry();
            interruptedStore.Upsert(new ActiveTurnState
            {
                SessionId = "bootstrap-recoverable",
                Prompt = "Resume the interrupted bootstrap turn.",
                TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "bootstrap-recoverable.jsonl"),
                WorkingDirectory = workspaceRoot,
                GitBranch = "main",
                ToolName = "edit",
                Stage = "response-delta",
                Status = "streaming",
                ContentSnapshot = "Partial bootstrap output.",
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                LastUpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });

            var service = new DesktopAppService(
                new LocaleStateService(shellOptions),
                new BootstrapProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    settingsResolver,
                    projectSummaryService,
                    toolRegistry,
                    toolExecutor,
                    channelRegistry,
                    extensionCatalog,
                    workspaceInspectionService,
                    authFlowService,
                    mcpConnectionManager,
                    modelRegistry,
                    transcriptStore,
                    activeTurnRegistry,
                    arenaSessionRegistry,
                    interruptedStore,
                    new ChatRecordingService(),
                    new FakeSessionTitleGenerationService(),
                    new LocaleStateService(shellOptions)),
                new ArenaProjectionService(arenaSessionRegistry),
                new AuthProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    authFlowService),
                new ChannelProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    channelRegistry),
                new WorkspaceProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    workspaceInspectionService,
                    new GitWorktreeService(new GitCliService(), runtimeProfileService, gitHistoryService),
                    gitHistoryService),
                new McpProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    mcpRegistry,
                    mcpConnectionManager),
                new PromptProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    promptRegistryService),
                new FollowupProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    new FollowupSuggestionService(
                        transcriptStore,
                        activeTurnRegistry,
                        interruptedStore,
                        arenaSessionRegistry,
                        runtimeProfileService)),
                new ExtensionProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    extensionCatalog),
                new SessionProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    transcriptStore,
                    sessionService,
                    toolExecutor,
                    CreateSessionHost(
                        runtimeProfileService,
                        compatibilityService,
                        transcriptStore,
                        activeTurnRegistry,
                        interruptedStore),
                    activeTurnRegistry,
                    runtimeProfileService,
                    new ServiceCollection()
                        .AddSingleton<ISessionTitleGenerationService>(new FakeSessionTitleGenerationService())
                        .BuildServiceProvider(),
                    new LocaleStateService(shellOptions)));

            var payload = await service.GetBootstrapAsync();

            var recoverableTurn = Assert.Single(payload.RecoverableTurns);
            Assert.Equal("bootstrap-recoverable", recoverableTurn.SessionId);
            Assert.Equal("edit", recoverableTurn.ToolName);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetBootstrapAsync_IncludesProjectSummaryFromWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-bootstrap-summary-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));

        try
        {
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "PROJECT_SUMMARY.md"),
                """
                **Update time**: 2026-04-02T12:00:00Z

                ## Overall Goal
                Keep qwen compatibility while moving the runtime to C#.

                ## Current Plan
                1. [IN PROGRESS] Integrate project summary parsing.
                2. [TODO] Surface welcome-back context.
                """
            );

            var service = CreateService(new DesktopShellOptions
            {
                Workspace = new WorkspacePaths
                {
                    WorkspaceRoot = workspaceRoot
                }
            });

            var payload = await service.GetBootstrapAsync();

            Assert.True(payload.ProjectSummary.HasHistory);
            Assert.Equal("Keep qwen compatibility while moving the runtime to C#.", payload.ProjectSummary.OverallGoal);
            Assert.Equal(2, payload.ProjectSummary.PendingTasks.Count);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetBootstrapAsync_AndGetActiveArenaSessionsAsync_IncludeLiveArenaStateAndForwardEvents()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-bootstrap-arena-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var approvalPolicyService = new ApprovalPolicyService();
            var workspacePathResolver = new WorkspacePathResolver(environmentPaths);
            var projectSummaryService = new ProjectSummaryService();
            var shellOptions = Microsoft.Extensions.Options.Options.Create(new DesktopShellOptions
            {
                Workspace = new WorkspacePaths
                {
                    WorkspaceRoot = workspaceRoot
                }
            });
            var settingsResolver = new DesktopSettingsResolver(
                compatibilityService,
                runtimeProfileService);
            var authFlowService = new AuthFlowService(
                runtimeProfileService,
                environmentPaths,
                new FileQwenOAuthCredentialStore(environmentPaths),
                new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))),
                new FakeAuthUrlLauncher(),
                new QwenOAuthTokenManager(
                    new FileQwenOAuthCredentialStore(environmentPaths),
                    environmentPaths,
                    new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));
            var toolRegistry = new ToolCatalogService(runtimeProfileService, approvalPolicyService);
            var toolExecutor = new NativeToolHostService(runtimeProfileService, approvalPolicyService);
            var workspaceInspectionService = new WorkspaceInspectionService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new FileDiscoveryService(new GitCliService(), runtimeProfileService));
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var channelRegistry = new ChannelRegistryService(
                environmentPaths,
                settingsResolver,
                extensionCatalog);
            var mcpTokenStore = new FileMcpTokenStore(environmentPaths);
            var mcpRegistry = new McpRegistryService(runtimeProfileService, mcpTokenStore);
            var mcpConnectionManager = new McpConnectionManagerService(
                mcpRegistry,
                new McpToolRuntimeService(mcpRegistry, mcpTokenStore, new HttpClient(), runtimeProfileService));
            var promptRegistryService = new PromptRegistryService(
                mcpConnectionManager,
                new McpToolRuntimeService(mcpRegistry, mcpTokenStore, new HttpClient(), runtimeProfileService));
            var gitHistoryService = new GitHistoryService(new GitCliService(), runtimeProfileService);
            var modelRegistry = new ModelRegistryService(
                new RuntimeConfigService(environmentPaths),
                new TokenLimitService(),
                Microsoft.Extensions.Options.Options.Create(new NativeAssistantRuntimeOptions()));
            var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessionService = (ISessionService)transcriptStore;
            var interruptedStore = new InterruptedTurnStore();
            var activeTurnRegistry = new ActiveTurnRegistry(interruptedStore);
            var arenaSessionRegistry = new ArenaSessionRegistry();

            arenaSessionRegistry.Start(
                new ActiveArenaSessionState
                {
                    SessionId = "arena-live-bootstrap",
                    Task = "Benchmark two models",
                    TaskId = "task-bootstrap-1",
                    Status = "running",
                    WorkingDirectory = workspaceRoot,
                    BaseBranch = "main",
                    RoundCount = 1,
                    StartedAtUtc = DateTime.UtcNow.AddMinutes(-3),
                    LastUpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                    Agents =
                    [
                        new ArenaAgentStatusFile
                        {
                            AgentId = "arena-live-bootstrap/model-a",
                            AgentName = "model-a",
                            Status = "running",
                            Model = "model-a",
                            WorktreeName = "model-a",
                            WorktreePath = Path.Combine(workspaceRoot, "worktrees", "model-a"),
                            Branch = "arena/model-a",
                            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
                        },
                        new ArenaAgentStatusFile
                        {
                            AgentId = "arena-live-bootstrap/model-b",
                            AgentName = "model-b",
                            Status = "running",
                            Model = "model-b",
                            WorktreeName = "model-b",
                            WorktreePath = Path.Combine(workspaceRoot, "worktrees", "model-b"),
                            Branch = "arena/model-b",
                            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
                        }
                    ]
                },
                new CancellationTokenSource(),
                "Arena started.");

            var service = new DesktopAppService(
                new LocaleStateService(shellOptions),
                new BootstrapProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    settingsResolver,
                    projectSummaryService,
                    toolRegistry,
                    toolExecutor,
                    channelRegistry,
                    extensionCatalog,
                    workspaceInspectionService,
                    authFlowService,
                    mcpConnectionManager,
                    modelRegistry,
                    transcriptStore,
                    activeTurnRegistry,
                    arenaSessionRegistry,
                    interruptedStore,
                    new ChatRecordingService(),
                    new FakeSessionTitleGenerationService(),
                    new LocaleStateService(shellOptions)),
                new ArenaProjectionService(arenaSessionRegistry),
                new AuthProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    authFlowService),
                new ChannelProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    channelRegistry),
                new WorkspaceProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    workspaceInspectionService,
                    new GitWorktreeService(new GitCliService(), runtimeProfileService, gitHistoryService),
                    gitHistoryService),
                new McpProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    mcpRegistry,
                    mcpConnectionManager),
                new PromptProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    promptRegistryService),
                new FollowupProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    new FollowupSuggestionService(
                        transcriptStore,
                        activeTurnRegistry,
                        interruptedStore,
                        arenaSessionRegistry,
                        runtimeProfileService)),
                new ExtensionProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    extensionCatalog),
                new SessionProjectionService(
                    shellOptions,
                    workspacePathResolver,
                    transcriptStore,
                    sessionService,
                    toolExecutor,
                    CreateSessionHost(
                        runtimeProfileService,
                        compatibilityService,
                        transcriptStore,
                        activeTurnRegistry,
                        interruptedStore),
                    activeTurnRegistry,
                    runtimeProfileService,
                    new ServiceCollection()
                        .AddSingleton<ISessionTitleGenerationService>(new FakeSessionTitleGenerationService())
                        .BuildServiceProvider(),
                    new LocaleStateService(shellOptions)));

            ArenaSessionEvent? observedEvent = null;
            service.ArenaEvent += (_, arenaEvent) => observedEvent = arenaEvent;

            var payload = await service.GetBootstrapAsync();
            var activeArenaSession = Assert.Single(payload.ActiveArenaSessions);
            Assert.Equal("arena-live-bootstrap", activeArenaSession.SessionId);
            Assert.Equal("task-bootstrap-1", activeArenaSession.TaskId);
            Assert.Equal("running", activeArenaSession.Status);
            Assert.Equal(2, activeArenaSession.Agents.Count);

            var activeArenaSessions = await service.GetActiveArenaSessionsAsync();
            Assert.Single(activeArenaSessions);
            Assert.Equal("arena-live-bootstrap", activeArenaSessions[0].SessionId);

            arenaSessionRegistry.Update(
                "arena-live-bootstrap",
                state => state.SelectedWinner = "model-a",
                ArenaSessionEventKind.SessionUpdated,
                "Winner selected.");

            Assert.NotNull(observedEvent);
            Assert.Equal(ArenaSessionEventKind.SessionUpdated, observedEvent!.Kind);
            Assert.Equal("task-bootstrap-1", observedEvent.TaskId);
            Assert.Equal("model-a", observedEvent.SelectedWinner);

            var cancelResult = await service.CancelArenaSessionAsync(new CancelArenaSessionRequest
            {
                SessionId = "arena-live-bootstrap"
            });

            Assert.True(cancelResult.WasCancelled);
            Assert.Contains("Cancellation requested", cancelResult.Message);
            Assert.Equal("cancelling", (await service.GetActiveArenaSessionsAsync()).Single().Status);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }


}

