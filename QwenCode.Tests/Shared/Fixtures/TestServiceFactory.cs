using Microsoft.Extensions.Options;
using QwenCode.App.Auth;
using QwenCode.App.Channels;
using QwenCode.App.Extensions;
using QwenCode.App.Infrastructure;

namespace QwenCode.Tests.Shared.Fixtures;

internal static class TestServiceFactory
{
    internal static DesktopAppService CreateService(DesktopShellOptions? options = null)
    {
        var environmentPaths = new FakeDesktopEnvironmentPaths(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.CurrentDirectory,
            AppContext.BaseDirectory);
        var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
        var approvalPolicyService = new ApprovalPolicyService();
        var workspacePathResolver = new WorkspacePathResolver(environmentPaths);
        var shellOptions = Options.Create(options ?? new DesktopShellOptions());
        var authHttpClient = new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var qwenOAuthCredentialStore = new FileQwenOAuthCredentialStore(environmentPaths);
        var qwenOAuthTokenManager = new QwenOAuthTokenManager(
            qwenOAuthCredentialStore,
            environmentPaths,
            authHttpClient);
        var authFlowService = new AuthFlowService(
            runtimeProfileService,
            environmentPaths,
            qwenOAuthCredentialStore,
            authHttpClient,
            new FakeAuthUrlLauncher(),
            qwenOAuthTokenManager);
        var compatibilityService = new QwenCompatibilityService(environmentPaths);
        var settingsResolver = new DesktopSettingsResolver(
            compatibilityService,
            runtimeProfileService);
        var projectSummaryService = new ProjectSummaryService();
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
        var mcpRegistry = new McpRegistryService(
            runtimeProfileService,
            new FileMcpTokenStore(environmentPaths));
        var mcpConnectionManager = new McpConnectionManagerService(
            mcpRegistry,
            new McpToolRuntimeService(
                mcpRegistry,
                new FileMcpTokenStore(environmentPaths),
                new HttpClient(),
                runtimeProfileService));
        var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService);
        var interruptedTurnStore = new InterruptedTurnStore();
        var activeTurnRegistry = new ActiveTurnRegistry(interruptedTurnStore);
        var arenaSessionRegistry = new ArenaSessionRegistry();
        var sessionHost = CreateSessionHost(
            runtimeProfileService,
            compatibilityService,
            transcriptStore,
            activeTurnRegistry,
            interruptedTurnStore);

        return new DesktopAppService(
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
                transcriptStore,
                activeTurnRegistry,
                arenaSessionRegistry,
                interruptedTurnStore),
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
                new GitWorktreeService(new GitCliService(), runtimeProfileService)),
            new McpProjectionService(
                shellOptions,
                workspacePathResolver,
                mcpRegistry,
                mcpConnectionManager),
            new ExtensionProjectionService(
                shellOptions,
                workspacePathResolver,
                extensionCatalog),
            new SessionProjectionService(
                shellOptions,
                workspacePathResolver,
                transcriptStore,
                toolExecutor,
                sessionHost,
                activeTurnRegistry));
    }

    internal static DesktopSessionHostService CreateSessionHost(
        QwenRuntimeProfileService runtimeProfileService,
        QwenCompatibilityService compatibilityService,
        ITranscriptStore? transcriptStore = null,
        IActiveTurnRegistry? activeTurnRegistry = null,
        IInterruptedTurnStore? interruptedTurnStore = null,
        IUserPromptHookService? userPromptHookService = null,
        IHookLifecycleService? hookLifecycleService = null)
    {
        var approvalPolicyService = new ApprovalPolicyService();
        var effectiveInterruptedTurnStore = interruptedTurnStore ?? new InterruptedTurnStore();
        var effectiveTranscriptStore = transcriptStore ?? new DesktopSessionCatalogService(runtimeProfileService);
        var effectiveUserQuestionToolService = new UserQuestionToolService();
        var pendingApprovalResolver = new PendingApprovalResolver();
        var sessionMessageBus = new SessionMessageBus(
            new PendingToolApprovalMessageHandler(
                effectiveTranscriptStore,
                pendingApprovalResolver,
                runtimeProfileService),
            new PendingQuestionAnswerMessageHandler(
                effectiveTranscriptStore,
                pendingApprovalResolver,
                runtimeProfileService,
                effectiveUserQuestionToolService));

        return new DesktopSessionHostService(
            runtimeProfileService,
            new CommandActionRuntime(
                new SlashCommandRuntime(compatibilityService),
                runtimeProfileService,
                compatibilityService,
                new ToolCatalogService(runtimeProfileService, approvalPolicyService)),
            CreateAssistantTurnRuntime(),
            new ChatCompressionService(),
            new NativeToolHostService(runtimeProfileService, approvalPolicyService),
            hookLifecycleService ?? new PassthroughHookLifecycleService(),
            effectiveUserQuestionToolService,
            userPromptHookService ?? new PassthroughUserPromptHookService(),
            effectiveTranscriptStore,
            activeTurnRegistry ?? new ActiveTurnRegistry(effectiveInterruptedTurnStore),
            effectiveInterruptedTurnStore,
            new SessionTranscriptWriter(),
            new SessionEventFactory(),
            sessionMessageBus);
    }

    internal static IAssistantTurnRuntime CreateAssistantTurnRuntime(
        IAssistantResponseProvider? primaryProvider = null,
        IToolExecutor? toolExecutor = null) =>
        new AssistantTurnRuntime(
            new AssistantPromptAssembler(new ProjectSummaryService()),
            primaryProvider is null
                ? [
                    new OpenAiCompatibleAssistantResponseProvider(
                        new HttpClient(),
                        new ProviderConfigurationResolver(
                            new FakeDesktopEnvironmentPaths(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)))),
                    new FallbackAssistantResponseProvider()
                ]
                : [primaryProvider, new FallbackAssistantResponseProvider()],
            toolExecutor ?? new FakeToolExecutor(),
            new LoopDetectionService(),
            Options.Create(new NativeAssistantRuntimeOptions
            {
                Provider = primaryProvider?.Name ?? "fallback"
            }));

    private sealed class PassthroughUserPromptHookService : IUserPromptHookService
    {
        public Task<UserPromptHookResult> ExecuteAsync(
            QwenRuntimeProfile runtimeProfile,
            UserPromptHookRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserPromptHookResult
            {
                EffectivePrompt = request.Prompt
            });
    }

    private sealed class PassthroughHookLifecycleService : IHookLifecycleService
    {
        public Task<HookLifecycleResult> ExecuteAsync(
            QwenRuntimeProfile runtimeProfile,
            HookInvocationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new HookLifecycleResult());
    }
}
