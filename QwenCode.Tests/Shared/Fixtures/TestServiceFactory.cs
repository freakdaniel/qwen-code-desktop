using Microsoft.Extensions.Options;
using QwenCode.App.Auth;

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
        var mcpRegistry = new McpRegistryService(
            runtimeProfileService,
            new FileMcpTokenStore(environmentPaths));
        var mcpConnectionManager = new McpConnectionManagerService(
            mcpRegistry,
            new McpToolRuntimeService(
                mcpRegistry,
                new FileMcpTokenStore(environmentPaths),
                new HttpClient()));
        var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService);
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
                authFlowService,
                mcpConnectionManager,
                transcriptStore,
                activeTurnRegistry,
                interruptedTurnStore),
            new AuthProjectionService(
                shellOptions,
                workspacePathResolver,
                authFlowService),
            new McpProjectionService(
                shellOptions,
                workspacePathResolver,
                mcpRegistry,
                mcpConnectionManager),
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
        IInterruptedTurnStore? interruptedTurnStore = null)
    {
        var approvalPolicyService = new ApprovalPolicyService();
        var effectiveInterruptedTurnStore = interruptedTurnStore ?? new InterruptedTurnStore();
        return new DesktopSessionHostService(
            runtimeProfileService,
            new CommandActionRuntime(
                new SlashCommandRuntime(compatibilityService),
                runtimeProfileService,
                compatibilityService,
                new ToolCatalogService(runtimeProfileService, approvalPolicyService)),
            CreateAssistantTurnRuntime(),
            new NativeToolHostService(runtimeProfileService, approvalPolicyService),
            new UserQuestionToolService(),
            transcriptStore ?? new DesktopSessionCatalogService(runtimeProfileService),
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
            Options.Create(new NativeAssistantRuntimeOptions
            {
                Provider = primaryProvider?.Name ?? "fallback"
            }));
}
