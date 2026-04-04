namespace QwenCode.Tests.Sessions;

public sealed class SessionMessageBusTests
{
    [Fact]
    public async Task SessionMessageBus_RequestPendingToolApprovalAsync_PreservesCorrelationAndResolvesPendingEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-bus-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "default",
                    "ask": ["Edit"]
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService);
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);
            var pendingApprovalResolver = new PendingApprovalResolver();
            var sessionMessageBus = new SessionMessageBus(
                new PendingToolApprovalMessageHandler(
                    sessionCatalog,
                    pendingApprovalResolver,
                    runtimeProfileService),
                new PendingQuestionAnswerMessageHandler(
                    sessionCatalog,
                    pendingApprovalResolver,
                    runtimeProfileService,
                    new UserQuestionToolService()));

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var startResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue a pending edit for bus resolution.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"bus write"}""",
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(pendingDetail);
            var pendingEntry = Assert.Single(
                pendingDetail!.Entries,
                entry => entry.Type == "tool" && entry.Status == "approval-required");

            var response = await sessionMessageBus.RequestPendingToolApprovalAsync(
                new PendingToolApprovalMessageRequest
                {
                    CorrelationId = "corr-tool-1",
                    Paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                    SessionId = startResult.Session.SessionId,
                    EntryId = pendingEntry.Id
                });

            Assert.Equal("corr-tool-1", response.CorrelationId);
            Assert.Equal(startResult.Session.SessionId, response.Detail.Session.SessionId);
            Assert.Equal(pendingEntry.Id, response.PendingTool.Id);
            Assert.Equal(workspaceRoot, response.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SessionMessageBus_RequestPendingQuestionAnswerAsync_ValidatesAnswersAndResolvesPendingEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-bus-question-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService);
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);
            var pendingApprovalResolver = new PendingApprovalResolver();
            var userQuestionToolService = new UserQuestionToolService();
            var sessionMessageBus = new SessionMessageBus(
                new PendingToolApprovalMessageHandler(
                    sessionCatalog,
                    pendingApprovalResolver,
                    runtimeProfileService),
                new PendingQuestionAnswerMessageHandler(
                    sessionCatalog,
                    pendingApprovalResolver,
                    runtimeProfileService,
                    userQuestionToolService));

            var startResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Ask the user which runtime path to continue.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "ask_user_question",
                    ToolArgumentsJson =
                        """
                        {
                          "questions": [
                            {
                              "header": "Direction",
                              "question": "Which runtime path should we continue?",
                              "multiSelect": false,
                              "options": [
                                { "label": "Bus parity", "description": "Continue message bus work." },
                                { "label": "UI polish", "description": "Pause runtime and polish the renderer." }
                              ]
                            }
                          ]
                        }
                        """,
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(pendingDetail);
            var pendingEntry = Assert.Single(
                pendingDetail!.Entries,
                entry => entry.Type == "tool" && entry.Status == "input-required");

            var response = await sessionMessageBus.RequestPendingQuestionAnswerAsync(
                new PendingQuestionAnswerMessageRequest
                {
                    CorrelationId = "corr-question-1",
                    Paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                    SessionId = startResult.Session.SessionId,
                    EntryId = pendingEntry.Id,
                    Answers =
                    [
                        new DesktopQuestionAnswer
                        {
                            QuestionIndex = 0,
                            Value = "Bus parity"
                        }
                    ]
                });

            Assert.Equal("corr-question-1", response.CorrelationId);
            Assert.Equal(startResult.Session.SessionId, response.Detail.Session.SessionId);
            Assert.Equal(pendingEntry.Id, response.PendingQuestion.Id);
            Assert.Equal(workspaceRoot, response.WorkingDirectory);
            Assert.Single(response.Questions);
            Assert.Single(response.Answers);
            Assert.Equal("Bus parity", response.Answers[0].Value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
