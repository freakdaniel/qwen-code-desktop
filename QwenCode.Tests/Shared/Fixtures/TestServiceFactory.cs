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
        var gitHistoryService = new GitHistoryService(new GitCliService(), runtimeProfileService);
        var workspaceInspectionService = new WorkspaceInspectionService(
            new GitWorktreeService(new GitCliService(), runtimeProfileService, gitHistoryService),
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
        var chatRecordingService = new ChatRecordingService();
        var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService, chatRecordingService);
        var sessionService = (ISessionService)transcriptStore;
        var interruptedTurnStore = new InterruptedTurnStore();
        var activeTurnRegistry = new ActiveTurnRegistry(interruptedTurnStore);
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
                interruptedTurnStore),
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
                sessionHost,
                activeTurnRegistry));
    }

    internal static DesktopSessionHostService CreateSessionHost(
        QwenRuntimeProfileService runtimeProfileService,
        QwenCompatibilityService compatibilityService,
        ITranscriptStore? transcriptStore = null,
        IActiveTurnRegistry? activeTurnRegistry = null,
        IInterruptedTurnStore? interruptedTurnStore = null,
        IUserPromptHookService? userPromptHookService = null)
    {
        var approvalPolicyService = new ApprovalPolicyService();
        var effectiveInterruptedTurnStore = interruptedTurnStore ?? new InterruptedTurnStore();
        var chatRecordingService = new ChatRecordingService();
        return new DesktopSessionHostService(
            runtimeProfileService,
            new CommandActionRuntime(
                new SlashCommandRuntime(compatibilityService),
                runtimeProfileService,
                compatibilityService,
                new ToolCatalogService(runtimeProfileService, approvalPolicyService)),
            CreateAssistantTurnRuntime(),
            new ChatCompressionService(),
            chatRecordingService,
            new NativeToolHostService(runtimeProfileService, approvalPolicyService),
            new PassthroughHookLifecycleService(),
            new UserQuestionToolService(),
            userPromptHookService ?? new PassthroughUserPromptHookService(),
            transcriptStore ?? new DesktopSessionCatalogService(runtimeProfileService, chatRecordingService),
            activeTurnRegistry ?? new ActiveTurnRegistry(effectiveInterruptedTurnStore),
            effectiveInterruptedTurnStore,
            new SessionTranscriptWriter(),
            new SessionEventFactory(),
            new PendingApprovalResolver());
    }

    internal static IAssistantTurnRuntime CreateAssistantTurnRuntime(
        IAssistantResponseProvider? primaryProvider = null,
        IToolExecutor? toolExecutor = null) =>
        new AssistantTurnRuntime(
            new AssistantPromptAssembler(
                new ProjectSummaryService(),
                new DesktopSessionCatalogService(
                    new QwenRuntimeProfileService(
                        new FakeDesktopEnvironmentPaths(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData))),
                    new ChatRecordingService())),
            primaryProvider is null
                ? [
                    new OpenAiCompatibleAssistantResponseProvider(
                        new HttpClient(),
                        new ProviderConfigurationResolver(
                            new FakeDesktopEnvironmentPaths(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData))),
                        new TokenLimitService()),
                    new FallbackAssistantResponseProvider()
                ]
                : [primaryProvider, new FallbackAssistantResponseProvider()],
            new ToolCallScheduler(
                new NonInteractiveToolExecutor(toolExecutor ?? new FakeToolExecutor()),
                new LoopDetectionService()),
            new LoopDetectionService(),
            new TokenLimitService(),
            new ProviderConfigurationResolver(
                new FakeDesktopEnvironmentPaths(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData))),
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
