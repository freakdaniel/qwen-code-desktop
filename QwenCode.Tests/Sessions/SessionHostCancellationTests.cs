namespace QwenCode.Tests.Sessions;

public sealed class SessionHostCancellationTests
{
    [Fact]
    public async Task DesktopSessionHostService_CancelTurnAsync_CancelsActiveTurnAndPublishesCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-cancel-turn-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var approvalPolicyService = new ApprovalPolicyService();
            var chatRecordingService = new ChatRecordingService();
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, chatRecordingService);
            var interruptedTurnStore = new InterruptedTurnStore();
            var sessionHost = new DesktopSessionHostService(
                runtimeProfileService,
                new CommandActionRuntime(
                    new SlashCommandRuntime(compatibilityService),
                    runtimeProfileService,
                    compatibilityService,
                    new ToolCatalogService(runtimeProfileService, approvalPolicyService)),
                CreateAssistantTurnRuntime(new CancellableAssistantResponseProvider()),
                new ChatCompressionService(),
                chatRecordingService,
                new NativeToolHostService(runtimeProfileService, approvalPolicyService),
                new PassthroughHookLifecycleService(),
                new UserQuestionToolService(),
                new PassthroughUserPromptHookService(),
                sessionCatalog,
                new ActiveTurnRegistry(interruptedTurnStore),
                interruptedTurnStore,
                new SessionTranscriptWriter(),
                new SessionEventFactory(),
                new PendingApprovalResolver());

            var emittedEvents = new List<DesktopSessionEvent>();
            var turnStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            sessionHost.SessionEvent += (_, sessionEvent) =>
            {
                emittedEvents.Add(sessionEvent);
                if (sessionEvent.Kind == DesktopSessionEventKind.TurnStarted)
                {
                    turnStarted.TrySetResult();
                }
            };

            var turnTask = sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    SessionId = "cancel-session",
                    Prompt = "Run a long native turn so we can cancel it.",
                    WorkingDirectory = workspaceRoot
                });

            await turnStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var cancelResult = await sessionHost.CancelTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new CancelDesktopSessionTurnRequest
                {
                    SessionId = "cancel-session"
                });
            var turnResult = await turnTask;

            Assert.True(cancelResult.Cancelled);
            Assert.Equal("cancel-session", turnResult.Session.SessionId);
            Assert.Equal("cancelled", turnResult.ToolExecution.Status);
            Assert.Contains("cancelled", turnResult.AssistantSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(DesktopSessionEventKind.TurnCancelled, emittedEvents[^1].Kind);

            var detail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = "cancel-session"
                });
            Assert.NotNull(detail);
            Assert.Contains(detail!.Entries, entry =>
                entry.Type == "assistant" &&
                entry.Body.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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


