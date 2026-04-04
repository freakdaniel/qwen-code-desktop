using QwenCode.App.Followup;

namespace QwenCode.Tests.Followup;

public sealed class FollowupSuggestionServiceTests
{
    [Fact]
    public async Task FollowupSuggestionService_GetSuggestionsAsync_UsesProviderBackedSuggestionWhenValid()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-followup-provider-{Guid.NewGuid():N}");
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
            var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService);
            var interruptedStore = new InterruptedTurnStore();
            var activeTurnRegistry = new ActiveTurnRegistry(interruptedStore);
            var arenaRegistry = new ArenaSessionRegistry();
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                transcriptStore,
                activeTurnRegistry,
                interruptedStore);
            var generator = new ProviderBackedFollowupSuggestionGenerator(
                runtimeProfileService,
                new AssistantPromptAssembler(new ProjectSummaryService()),
                [
                    new StaticAssistantResponseProvider(
                        "qwen-compatible",
                        static (_, _) => new AssistantTurnResponse
                        {
                            Summary = "run the tests",
                            ProviderName = "qwen-compatible",
                            Model = "qwen3-coder-plus"
                        }),
                    new FallbackAssistantResponseProvider()
                ],
                Options.Create(new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible"
                }));
            var service = new FollowupSuggestionService(
                transcriptStore,
                activeTurnRegistry,
                interruptedStore,
                arenaRegistry,
                runtimeProfileService,
                generator);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var turnResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Write the implementation notes.",
                    SessionId = "followup-provider",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"implementation notes"}""",
                    ApproveToolExecution = true
                });

            var snapshot = await service.GetSuggestionsAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetFollowupSuggestionsRequest
                {
                    SessionId = turnResult.Session.SessionId
                });

            Assert.NotEmpty(snapshot.Suggestions);
            Assert.Equal("run the tests", snapshot.Suggestions[0].Text);
            Assert.Equal("qwen-compatible", snapshot.Suggestions[0].Source);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FollowupSuggestionService_GetSuggestionsAsync_FallsBackWhenProviderSuggestionIsFiltered()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-followup-provider-filter-{Guid.NewGuid():N}");
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
            var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService);
            var interruptedStore = new InterruptedTurnStore();
            var activeTurnRegistry = new ActiveTurnRegistry(interruptedStore);
            var arenaRegistry = new ArenaSessionRegistry();
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                transcriptStore,
                activeTurnRegistry,
                interruptedStore);
            var generator = new ProviderBackedFollowupSuggestionGenerator(
                runtimeProfileService,
                new AssistantPromptAssembler(new ProjectSummaryService()),
                [
                    new StaticAssistantResponseProvider(
                        "qwen-compatible",
                        static (_, _) => new AssistantTurnResponse
                        {
                            Summary = "Looks good",
                            ProviderName = "qwen-compatible",
                            Model = "qwen3-coder-plus"
                        }),
                    new FallbackAssistantResponseProvider()
                ],
                Options.Create(new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible"
                }));
            var service = new FollowupSuggestionService(
                transcriptStore,
                activeTurnRegistry,
                interruptedStore,
                arenaRegistry,
                runtimeProfileService,
                generator);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var turnResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Write the implementation notes.",
                    SessionId = "followup-provider-filter",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"implementation notes"}""",
                    ApproveToolExecution = true
                });

            var snapshot = await service.GetSuggestionsAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetFollowupSuggestionsRequest
                {
                    SessionId = turnResult.Session.SessionId
                });

            Assert.DoesNotContain(snapshot.Suggestions, item => string.Equals(item.Source, "qwen-compatible", StringComparison.Ordinal));
            Assert.Contains(snapshot.Suggestions, item => item.Text == "review the changes");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FollowupSuggestionService_GetSuggestionsAsync_SuggestsPendingApprovalForApprovalRequiredTool()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-followup-approval-{Guid.NewGuid():N}");
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
            var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService);
            var interruptedStore = new InterruptedTurnStore();
            var activeTurnRegistry = new ActiveTurnRegistry(interruptedStore);
            var arenaRegistry = new ArenaSessionRegistry();
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                transcriptStore,
                activeTurnRegistry,
                interruptedStore);
            var service = new FollowupSuggestionService(
                transcriptStore,
                activeTurnRegistry,
                interruptedStore,
                arenaRegistry,
                runtimeProfileService);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var turnResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue an edit that needs approval.",
                    SessionId = "followup-approval",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"needs approval"}""",
                    ApproveToolExecution = false
                });

            var snapshot = await service.GetSuggestionsAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetFollowupSuggestionsRequest
                {
                    SessionId = turnResult.Session.SessionId
                });

            Assert.Equal(turnResult.Session.SessionId, snapshot.SessionId);
            Assert.Contains(snapshot.Suggestions, item => item.Text == "approve the pending tool");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FollowupSuggestionService_GetSuggestionsAsync_SuggestsReviewAndTestsAfterChangedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-followup-review-{Guid.NewGuid():N}");
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
            var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService);
            var interruptedStore = new InterruptedTurnStore();
            var activeTurnRegistry = new ActiveTurnRegistry(interruptedStore);
            var arenaRegistry = new ArenaSessionRegistry();
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                transcriptStore,
                activeTurnRegistry,
                interruptedStore);
            var service = new FollowupSuggestionService(
                transcriptStore,
                activeTurnRegistry,
                interruptedStore,
                arenaRegistry,
                runtimeProfileService);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var turnResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Write the implementation notes.",
                    SessionId = "followup-review",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"implementation notes"}""",
                    ApproveToolExecution = true
                });

            var snapshot = await service.GetSuggestionsAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetFollowupSuggestionsRequest
                {
                    SessionId = turnResult.Session.SessionId
                });

            Assert.Equal(turnResult.Session.SessionId, snapshot.SessionId);
            Assert.Contains(snapshot.Suggestions, item => item.Text == "review the changes");
            Assert.Contains(snapshot.Suggestions, item => item.Text == "run the tests");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
