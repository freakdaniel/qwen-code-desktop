using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.Core.Auth;
using QwenCode.Core.Channels;
using QwenCode.App.Desktop.Projection;
using QwenCode.Core.Extensions;
using QwenCode.Core.Followup;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Prompts;
using QwenCode.Core.Config;
using QwenCode.Core.Telemetry;
using QwenCode.Tests.Shared.Fakes;

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
        var approvalSessionRuleStore = new ApprovalSessionRuleStore();
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
        var toolExecutor = new NativeToolHostService(
            runtimeProfileService,
            approvalPolicyService,
            approvalSessionRuleStore: approvalSessionRuleStore);
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
        var promptRegistryService = new PromptRegistryService(
            mcpConnectionManager,
            new McpToolRuntimeService(
                mcpRegistry,
                new FileMcpTokenStore(environmentPaths),
                new HttpClient(),
                runtimeProfileService));
        var mcpResourceRegistryService = new McpResourceRegistryService(
            mcpConnectionManager,
            new McpToolRuntimeService(
                mcpRegistry,
                new FileMcpTokenStore(environmentPaths),
                new HttpClient(),
                runtimeProfileService));
        var interruptedTurnStore = new InterruptedTurnStore();
        var activeTurnRegistry = new ActiveTurnRegistry(interruptedTurnStore);
        var arenaSessionRegistry = new ArenaSessionRegistry();
        var sessionHost = CreateSessionHost(
            runtimeProfileService,
            compatibilityService,
            transcriptStore,
            activeTurnRegistry,
            interruptedTurnStore);
        var followupSuggestionService = new FollowupSuggestionService(
            transcriptStore,
            activeTurnRegistry,
            interruptedTurnStore,
            arenaSessionRegistry,
            runtimeProfileService,
            new ProviderBackedFollowupSuggestionGenerator(
                runtimeProfileService,
                new AssistantPromptAssembler(projectSummaryService),
                new StaticContentGenerator(static _ => null),
                Options.Create(new NativeAssistantRuntimeOptions
                {
                    Provider = "fallback"
                })));

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
                new ModelRegistryService(
                    new RuntimeConfigService(environmentPaths),
                    new TokenLimitService(),
                    Options.Create(new NativeAssistantRuntimeOptions())),
                transcriptStore,
                activeTurnRegistry,
                arenaSessionRegistry,
                interruptedTurnStore,
                chatRecordingService,
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
            new McpResourceProjectionService(
                shellOptions,
                workspacePathResolver,
                mcpResourceRegistryService),
            new FollowupProjectionService(
                shellOptions,
                workspacePathResolver,
                followupSuggestionService),
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
                activeTurnRegistry,
                runtimeProfileService,
                new ServiceCollection()
                    .AddSingleton<ISessionTitleGenerationService>(new FakeSessionTitleGenerationService())
                    .BuildServiceProvider(),
                new LocaleStateService(shellOptions)));
    }

    internal static DesktopSessionHostService CreateSessionHost(
        QwenRuntimeProfileService runtimeProfileService,
        QwenCompatibilityService compatibilityService,
        ITranscriptStore? transcriptStore = null,
        IActiveTurnRegistry? activeTurnRegistry = null,
        IInterruptedTurnStore? interruptedTurnStore = null,
        IUserPromptHookService? userPromptHookService = null,
        IHookLifecycleService? hookLifecycleService = null,
        ITelemetryService? telemetryService = null)
    {
        var approvalPolicyService = new ApprovalPolicyService();
        var approvalSessionRuleStore = new ApprovalSessionRuleStore();
        var effectiveInterruptedTurnStore = interruptedTurnStore ?? new InterruptedTurnStore();
        var chatRecordingService = new ChatRecordingService();
        var effectiveUserQuestionToolService = new UserQuestionToolService();
        var effectiveTranscriptStore = transcriptStore ?? new DesktopSessionCatalogService(runtimeProfileService, chatRecordingService);
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
            chatRecordingService,
            new NativeToolHostService(
                runtimeProfileService,
                approvalPolicyService,
                telemetryService: telemetryService,
                approvalSessionRuleStore: approvalSessionRuleStore),
            hookLifecycleService ?? new PassthroughHookLifecycleService(),
            effectiveUserQuestionToolService,
            userPromptHookService ?? new PassthroughUserPromptHookService(),
            effectiveTranscriptStore,
            activeTurnRegistry ?? new ActiveTurnRegistry(effectiveInterruptedTurnStore),
            effectiveInterruptedTurnStore,
            new SessionTranscriptWriter(),
            new SessionEventFactory(),
            sessionMessageBus,
            telemetryService,
            approvalSessionRuleStore: approvalSessionRuleStore);
    }

    internal static IAssistantTurnRuntime CreateAssistantTurnRuntime(
        IAssistantResponseProvider? primaryProvider = null,
        IToolExecutor? toolExecutor = null)
    {
        var environmentPaths = new FakeDesktopEnvironmentPaths(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        var configService = new RuntimeConfigService(environmentPaths);
        var modelConfigResolver = new ModelConfigResolver(
            new ModelRegistryService(
                configService,
                new TokenLimitService(),
                Options.Create(new NativeAssistantRuntimeOptions())));

        var providers = primaryProvider is null
            ? new IAssistantResponseProvider[]
            {
                new OpenAiCompatibleAssistantResponseProvider(
                    new HttpClient(),
                    new ProviderConfigurationResolver(
                        environmentPaths,
                        configService: configService,
                        modelConfigResolver: modelConfigResolver),
                    new TokenLimitService()),
                new FallbackAssistantResponseProvider()
            }
            : [primaryProvider, new FallbackAssistantResponseProvider()];

        return new AssistantTurnRuntime(
            new AssistantPromptAssembler(
                new ProjectSummaryService(),
                new DesktopSessionCatalogService(
                    new QwenRuntimeProfileService(environmentPaths),
                    new ChatRecordingService())),
            providers,
            new ToolCallScheduler(
                new NonInteractiveToolExecutor(toolExecutor ?? new FakeToolExecutor()),
                new LoopDetectionService()),
            new LoopDetectionService(),
            new TokenLimitService(),
            new ProviderConfigurationResolver(
                environmentPaths,
                configService: configService,
                modelConfigResolver: modelConfigResolver),
            Options.Create(new NativeAssistantRuntimeOptions
            {
                Provider = primaryProvider?.Name ?? "fallback"
            }));
    }

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
