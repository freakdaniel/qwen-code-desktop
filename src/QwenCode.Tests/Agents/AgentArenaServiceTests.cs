namespace QwenCode.Tests.Agents;

public sealed class AgentArenaServiceTests
{
    [Fact]
    public async Task AgentArenaService_ExecuteAsync_CreatesManagedWorktreesAndPersistsArenaArtifact()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-arena-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "arena");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtime = new RecordingArenaTurnRuntime();
            var registry = new ArenaSessionRegistry();
            var service = new AgentArenaService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new GitCliService(),
                runtimeProfileService,
                new StaticServiceProvider(runtime),
                registry);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var events = new List<AssistantRuntimeEvent>();

            using (var taskArguments = JsonDocument.Parse(
                       """
                       {
                         "subject":"Implement the strongest possible change",
                         "description":"Track the arena comparison task"
                       }
                       """))
            {
                var createdTask = await TaskStore.CreateTaskAsync(runtimeProfile, taskArguments.RootElement, CancellationToken.None);
                Assert.Equal("1", createdTask.Task.Id);
            }

            using var arguments = JsonDocument.Parse(
                """
                {
                  "session_id": "arena-test-session",
                  "task": "Implement the strongest possible change",
                  "task_id": "1",
                  "models": [
                    { "model": "model-alpha", "display_name": "Alpha" },
                    { "model": "model-beta", "display_name": "Beta" }
                  ]
                }
                """);

            var result = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                arguments.RootElement,
                "allow",
                events.Add);

            Assert.Equal("completed", result.Status);
            Assert.Equal("arena", result.ToolName);
            Assert.Contains("arena-test-session", result.Output);
            Assert.Contains("[Alpha]", result.Output);
            Assert.Contains("[Beta]", result.Output);
            Assert.Equal(2, runtime.Requests.Count);
            Assert.All(runtime.Requests, static request => Assert.Equal(AssistantPromptMode.ArenaCompetitor, request.PromptMode));
            Assert.All(runtime.Requests, static request => Assert.Contains("Arena rules:", request.SystemPromptOverride, StringComparison.Ordinal));
            Assert.All(runtime.Requests, static request => Assert.Contains("Work only inside your assigned worktree", request.SystemPromptOverride, StringComparison.Ordinal));
            Assert.All(runtime.Requests, static request => Assert.Contains("Do not mention the competition", request.SystemPromptOverride, StringComparison.Ordinal));
            Assert.All(runtime.Requests, static request => Assert.Contains("Success criteria:", request.Prompt, StringComparison.Ordinal));
            Assert.All(runtime.Requests, static request => Assert.Contains("validation performed, tradeoffs, and residual risks", request.Prompt, StringComparison.Ordinal));

            var artifactPath = result.ChangedFiles.Single(path => path.EndsWith("result.json", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(artifactPath));
            var sessionDirectory = Path.GetDirectoryName(artifactPath)!;
            var configPath = Path.Combine(sessionDirectory, "config.json");
            var statusPath = Path.Combine(sessionDirectory, "status.json");
            Assert.True(File.Exists(configPath));
            Assert.True(File.Exists(statusPath));

            var transcripts = result.ChangedFiles.Where(path => path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.Equal(2, transcripts.Length);
            Assert.All(transcripts, static path => Assert.True(File.Exists(path)));

            var persisted = await File.ReadAllTextAsync(artifactPath);
            Assert.Contains("\"SessionId\": \"arena-test-session\"", persisted);
            Assert.Contains("\"Model\": \"model-alpha\"", persisted);
            Assert.Contains("\"Model\": \"model-beta\"", persisted);
            var persistedConfig = await File.ReadAllTextAsync(configPath);
            var persistedStatus = await File.ReadAllTextAsync(statusPath);
            Assert.Contains("\"ArenaSessionId\": \"arena-test-session\"", persistedConfig);
            Assert.Contains("\"Task\": \"Implement the strongest possible change\"", persistedConfig);
            Assert.Contains("\"TaskId\": \"1\"", persistedConfig);
            Assert.Contains("\"RoundCount\": 1", persistedConfig);
            Assert.Contains("\"TaskId\": \"1\"", persisted);
            Assert.Contains("\"Status\": \"idle\"", persistedStatus);
            Assert.Contains("\"AgentName\": \"Alpha\"", persistedStatus);
            Assert.Contains("\"StopReason\": \"completed\"", persistedStatus);
            Assert.Contains("\"ToolCallCount\": 0", persistedStatus);
            Assert.Contains("\"AgentCount\": 2", persistedStatus);
            Assert.Contains("\"RoundCount\": 1", persistedStatus);

            var taskFilePath = TaskStore.ResolveTaskFilePath(runtimeProfile, null);
            var taskFile = await File.ReadAllTextAsync(taskFilePath);
            Assert.Contains("\"Id\": \"1\"", taskFile);
            Assert.Contains("\"Status\": \"in_progress\"", taskFile);
            Assert.Contains("\"Owner\": \"arena:arena-test-session\"", taskFile);

            using var statusArguments = JsonDocument.Parse(
                """
                {
                  "action": "status",
                  "session_id": "arena-test-session"
                }
                """);
            var statusResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                statusArguments.RootElement,
                "allow");
            Assert.Equal("completed", statusResult.Status);
            Assert.Contains("\"SessionId\": \"arena-test-session\"", statusResult.Output);
            Assert.Contains("\"Status\": \"idle\"", statusResult.Output);

            var snapshot = new GitWorktreeService(new GitCliService(), runtimeProfileService).Inspect(new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            });
            Assert.Contains(snapshot.Worktrees, item => item.IsManaged && item.SessionId == "arena-test-session" && item.Name == "Alpha");
            Assert.Contains(snapshot.Worktrees, item => item.IsManaged && item.SessionId == "arena-test-session" && item.Name == "Beta");

            Assert.Contains(events, item => item.Stage == "arena-agent-started" && item.AgentName == "Alpha");
            Assert.Contains(events, item => item.Stage == "arena-agent-completed" && item.AgentName == "Beta");
            Assert.Contains(events, item => item.Stage == "generating" && (item.AgentName == "Alpha" || item.AgentName == "Beta"));
        }
        finally
        {
            TryRunGit(workspaceRoot, "worktree", "remove", "--force", Path.Combine(homeRoot, ".qwen", "worktrees", "arena-test-session", "worktrees", "Alpha"));
            TryRunGit(workspaceRoot, "worktree", "remove", "--force", Path.Combine(homeRoot, ".qwen", "worktrees", "arena-test-session", "worktrees", "Beta"));
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    [Fact]
    public async Task AgentArenaService_ExecuteAsync_CleansUpManagedSessionViaCleanupAction()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-arena-cleanup-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "arena");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtime = new RecordingArenaTurnRuntime();
            var registry = new ArenaSessionRegistry();
            var service = new AgentArenaService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new GitCliService(),
                runtimeProfileService,
                new StaticServiceProvider(runtime),
                registry);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            using var arguments = JsonDocument.Parse(
                """
                {
                  "session_id": "arena-cleanup-session",
                  "task": "Try cleanup",
                  "models": ["model-a", "model-b"]
                }
                """);

            var result = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                arguments.RootElement,
                "allow");

            Assert.Equal("completed", result.Status);
            var sessionDirectory = Path.Combine(homeRoot, ".qwen", "arena", "arena-cleanup-session");
            Assert.True(Directory.Exists(sessionDirectory));

            using var cleanupArguments = JsonDocument.Parse(
                """
                {
                  "action": "cleanup",
                  "session_id": "arena-cleanup-session"
                }
                """);
            var cleanupResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                cleanupArguments.RootElement,
                "allow");
            Assert.Equal("completed", cleanupResult.Status);
            Assert.Contains("cleaned up", cleanupResult.Output);

            var snapshot = new GitWorktreeService(new GitCliService(), runtimeProfileService).Inspect(new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            });
            Assert.DoesNotContain(snapshot.Worktrees, item => item.IsManaged && item.SessionId == "arena-cleanup-session");
            Assert.False(Directory.Exists(sessionDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    [Fact]
    public async Task AgentArenaService_ExecuteAsync_FollowUpAndWinnerSelection_UpdateSessionState()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-arena-followup-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "arena");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtime = new RecordingArenaTurnRuntime();
            var registry = new ArenaSessionRegistry();
            var service = new AgentArenaService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new GitCliService(),
                runtimeProfileService,
                new StaticServiceProvider(runtime),
                registry);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            using var startArguments = JsonDocument.Parse(
                """
                {
                  "session_id": "arena-round-session",
                  "task": "Initial arena task",
                  "models": ["model-a", "model-b"]
                }
                """);
            var initialResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                startArguments.RootElement,
                "allow");
            Assert.Equal("completed", initialResult.Status);

            using var followUpArguments = JsonDocument.Parse(
                """
                {
                  "action": "follow_up",
                  "session_id": "arena-round-session",
                  "task": "Follow-up arena task"
                }
                """);
            var followUpResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                followUpArguments.RootElement,
                "allow");
            Assert.Equal("completed", followUpResult.Status);
            Assert.Contains("Round: 2", followUpResult.Output);

            using var winnerArguments = JsonDocument.Parse(
                """
                {
                  "action": "select_winner",
                  "session_id": "arena-round-session",
                  "winner": "model-a"
                }
                """);
            var winnerResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                winnerArguments.RootElement,
                "allow");
            Assert.Equal("completed", winnerResult.Status);

            var sessionDirectory = Path.Combine(homeRoot, ".qwen", "arena", "arena-round-session");
            var resultPath = Path.Combine(sessionDirectory, "result.json");
            var configPath = Path.Combine(sessionDirectory, "config.json");
            var statusPath = Path.Combine(sessionDirectory, "status.json");

            var persistedResult = await File.ReadAllTextAsync(resultPath);
            var persistedConfig = await File.ReadAllTextAsync(configPath);
            var persistedStatus = await File.ReadAllTextAsync(statusPath);
            Assert.Contains("\"RoundCount\": 2", persistedResult);
            Assert.Contains("\"SelectedWinner\": \"model-a\"", persistedResult);
            Assert.Contains("\"RoundCount\": 2", persistedConfig);
            Assert.Contains("\"SelectedWinner\": \"model-a\"", persistedConfig);
            Assert.Contains("\"RoundCount\": 2", persistedStatus);
            Assert.Contains("\"SelectedWinner\": \"model-a\"", persistedStatus);
            Assert.Contains("\"Task\": \"Follow-up arena task\"", persistedStatus);
            Assert.Contains("\"Status\": \"idle\"", persistedStatus);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    [Fact]
    public async Task AgentArenaService_ExecuteAsync_TracksLiveActiveArenaSessionWhileRunning()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-arena-live-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "arena");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtime = new BlockingArenaTurnRuntime(expectedCalls: 2);
            var registry = new ArenaSessionRegistry();
            var service = new AgentArenaService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new GitCliService(),
                runtimeProfileService,
                new StaticServiceProvider(runtime),
                registry);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            using var arguments = JsonDocument.Parse(
                """
                {
                  "session_id": "arena-live-session",
                  "task": "Observe live arena state",
                  "models": ["model-a", "model-b"]
                }
                """);

            var executionTask = service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                arguments.RootElement,
                "allow");

            await runtime.WaitUntilStartedAsync();
            await WaitForConditionAsync(() => registry.ListActiveSessions().Count == 1);

            var activeSession = registry.ListActiveSessions().Single();
            Assert.Equal("arena-live-session", activeSession.SessionId);
            Assert.Equal("running", activeSession.Status);
            Assert.Equal(1, activeSession.RoundCount);
            Assert.Equal(2, activeSession.Stats.AgentCount);
            Assert.Equal(2, activeSession.Agents.Count);
            Assert.Contains(activeSession.Agents, item => item.AgentName == "model-a" && item.Status == "running");
            Assert.Contains(activeSession.Agents, item => item.AgentName == "model-b" && item.Status == "running");

            using var statusArguments = JsonDocument.Parse(
                """
                {
                  "action": "status",
                  "session_id": "arena-live-session"
                }
                """);
            var liveStatus = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                statusArguments.RootElement,
                "allow");
            Assert.Equal("completed", liveStatus.Status);
            Assert.Contains("\"SessionId\": \"arena-live-session\"", liveStatus.Output);
            Assert.Contains("\"Status\": \"running\"", liveStatus.Output);
            Assert.Contains("\"AgentCount\": 2", liveStatus.Output);

            runtime.Release();
            var result = await executionTask;

            Assert.Equal("completed", result.Status);
            Assert.Empty(registry.ListActiveSessions());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    [Fact]
    public async Task AgentArenaService_ExecuteAsync_CancelAction_CancelsRunningArenaAndPersistsCancelledState()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-arena-cancel-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "arena");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtime = new BlockingArenaTurnRuntime(expectedCalls: 2);
            var registry = new ArenaSessionRegistry();
            var service = new AgentArenaService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new GitCliService(),
                runtimeProfileService,
                new StaticServiceProvider(runtime),
                registry);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            using var arguments = JsonDocument.Parse(
                """
                {
                  "session_id": "arena-cancel-session",
                  "task": "Cancel the arena round",
                  "models": ["model-a", "model-b"]
                }
                """);

            var executionTask = service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                arguments.RootElement,
                "allow");

            await runtime.WaitUntilStartedAsync();
            await WaitForConditionAsync(() => registry.ListActiveSessions().Count == 1);

            using var cancelArguments = JsonDocument.Parse(
                """
                {
                  "action": "cancel",
                  "session_id": "arena-cancel-session"
                }
                """);
            var cancelResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                cancelArguments.RootElement,
                "allow");

            Assert.Equal("completed", cancelResult.Status);
            Assert.Contains("Cancellation requested", cancelResult.Output);

            var finalResult = await executionTask;
            Assert.Equal("cancelled", finalResult.Status);
            Assert.Equal("Arena session was cancelled.", finalResult.ErrorMessage);
            Assert.Empty(registry.ListActiveSessions());

            var resultPath = Path.Combine(homeRoot, ".qwen", "arena", "arena-cancel-session", "result.json");
            var statusPath = Path.Combine(homeRoot, ".qwen", "arena", "arena-cancel-session", "status.json");
            Assert.Contains("\"Status\": \"cancelled\"", await File.ReadAllTextAsync(resultPath));
            var persistedStatus = await File.ReadAllTextAsync(statusPath);
            Assert.Contains("\"Status\": \"cancelled\"", persistedStatus);
            Assert.Contains("\"StopReason\": \"cancelled\"", persistedStatus);
            Assert.Contains("\"FailedAgentCount\":", persistedStatus);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    [Fact]
    public async Task AgentArenaService_ExecuteAsync_ApplyWinner_CopiesWinnerChangesBackToWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-arena-apply-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            var readmePath = Path.Combine(workspaceRoot, "README.md");
            File.WriteAllText(readmePath, "original");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtime = new ApplyingArenaTurnRuntime();
            var registry = new ArenaSessionRegistry();
            var service = new AgentArenaService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new GitCliService(),
                runtimeProfileService,
                new StaticServiceProvider(runtime),
                registry);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            using (var taskArguments = JsonDocument.Parse(
                       """
                       {
                         "subject":"Produce competing workspace edits",
                         "description":"Track the arena apply-winner flow"
                       }
                       """))
            {
                var createdTask = await TaskStore.CreateTaskAsync(runtimeProfile, taskArguments.RootElement, CancellationToken.None);
                Assert.Equal("1", createdTask.Task.Id);
            }

            using var startArguments = JsonDocument.Parse(
                """
                {
                  "session_id": "arena-apply-session",
                  "task": "Produce competing workspace edits",
                  "task_id": "1",
                  "models": ["model-a", "model-b"]
                }
                """);
            var initialResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                startArguments.RootElement,
                "allow");
            Assert.Equal("completed", initialResult.Status);

            using var winnerArguments = JsonDocument.Parse(
                """
                {
                  "action": "select_winner",
                  "session_id": "arena-apply-session",
                  "winner": "model-a"
                }
                """);
            var winnerResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                winnerArguments.RootElement,
                "allow");
            Assert.Equal("completed", winnerResult.Status);

            using var applyArguments = JsonDocument.Parse(
                """
                {
                  "action": "apply_winner",
                  "session_id": "arena-apply-session"
                }
                """);
            var applyResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                applyArguments.RootElement,
                "allow");

            Assert.Equal("completed", applyResult.Status);
            Assert.Contains("Applied arena winner 'model-a'", applyResult.Output);
            Assert.Equal("model-a applied change", await File.ReadAllTextAsync(readmePath));
            Assert.True(File.Exists(Path.Combine(workspaceRoot, "notes-model-a.md")));

            var sessionDirectory = Path.Combine(homeRoot, ".qwen", "arena", "arena-apply-session");
            var resultPath = Path.Combine(sessionDirectory, "result.json");
            var configPath = Path.Combine(sessionDirectory, "config.json");
            var statusPath = Path.Combine(sessionDirectory, "status.json");
            Assert.Contains("\"Status\": \"completed\"", await File.ReadAllTextAsync(resultPath));
            Assert.Contains("\"AppliedWinner\": \"model-a\"", await File.ReadAllTextAsync(resultPath));
            Assert.Contains("\"AppliedWinner\": \"model-a\"", await File.ReadAllTextAsync(configPath));
            Assert.Contains("\"AppliedWinner\": \"model-a\"", await File.ReadAllTextAsync(statusPath));
            Assert.Contains("\"Status\": \"completed\"", await File.ReadAllTextAsync(statusPath));

            var taskFilePath = TaskStore.ResolveTaskFilePath(runtimeProfile, null);
            var taskFile = await File.ReadAllTextAsync(taskFilePath);
            Assert.Contains("\"Id\": \"1\"", taskFile);
            Assert.Contains("\"Status\": \"completed\"", taskFile);
            Assert.Contains("\"Owner\": \"model-a\"", taskFile);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    [Fact]
    public async Task AgentArenaService_ExecuteAsync_Discard_PreservesArtifactsAndRemovesManagedWorktrees()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-arena-discard-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "arena");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtime = new RecordingArenaTurnRuntime();
            var registry = new ArenaSessionRegistry();
            var service = new AgentArenaService(
                new GitWorktreeService(new GitCliService(), runtimeProfileService),
                new GitCliService(),
                runtimeProfileService,
                new StaticServiceProvider(runtime),
                registry);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            using var startArguments = JsonDocument.Parse(
                """
                {
                  "session_id": "arena-discard-session",
                  "task": "Discard this arena session",
                  "models": ["model-a", "model-b"]
                }
                """);
            var initialResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                startArguments.RootElement,
                "allow");
            Assert.Equal("completed", initialResult.Status);

            using var discardArguments = JsonDocument.Parse(
                """
                {
                  "action": "discard",
                  "session_id": "arena-discard-session"
                }
                """);
            var discardResult = await service.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                discardArguments.RootElement,
                "allow");
            Assert.Equal("completed", discardResult.Status);
            Assert.Contains("Discarded arena session 'arena-discard-session'", discardResult.Output);

            var sessionDirectory = Path.Combine(homeRoot, ".qwen", "arena", "arena-discard-session");
            Assert.True(Directory.Exists(sessionDirectory));
            var resultPath = Path.Combine(sessionDirectory, "result.json");
            var statusPath = Path.Combine(sessionDirectory, "status.json");
            Assert.Contains("\"Status\": \"discarded\"", await File.ReadAllTextAsync(resultPath));
            Assert.Contains("\"Status\": \"discarded\"", await File.ReadAllTextAsync(statusPath));

            var snapshot = new GitWorktreeService(new GitCliService(), runtimeProfileService).Inspect(new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            });
            Assert.DoesNotContain(snapshot.Worktrees, item => item.IsManaged && item.SessionId == "arena-discard-session");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var result = new GitCliService().Run(workingDirectory, arguments);
        Assert.True(result.Success, result.StandardError);
    }

    private static void TryRunGit(string workingDirectory, params string[] arguments)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return;
        }

        _ = new GitCliService().Run(workingDirectory, arguments);
    }

    private static void DeleteDirectory(string root)
    {
        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }

    private sealed class RecordingArenaTurnRuntime : IAssistantTurnRuntime
    {
        public List<AssistantTurnRequest> Requests { get; } = [];

        public Task<AssistantTurnResponse> GenerateAsync(
            AssistantTurnRequest request,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "generating",
                ProviderName = "fake-arena",
                Message = $"Generating for {request.ModelOverride}."
            });

            var fileName = $"notes-{request.ModelOverride}.md";
            File.WriteAllText(
                Path.Combine(request.WorkingDirectory, fileName),
                $"Model {request.ModelOverride} completed arena task.");

            return Task.FromResult(new AssistantTurnResponse
            {
                Summary = $"{request.ModelOverride} completed arena task.",
                ProviderName = "fake-arena",
                Model = request.ModelOverride,
                ToolExecutions = []
            });
        }
    }

    private sealed class BlockingArenaTurnRuntime(int expectedCalls) : IAssistantTurnRuntime
    {
        private readonly TaskCompletionSource _allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;

        public async Task<AssistantTurnResponse> GenerateAsync(
            AssistantTurnRequest request,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default)
        {
            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "generating",
                ProviderName = "fake-arena",
                Message = $"Generating for {request.ModelOverride}."
            });

            if (Interlocked.Increment(ref _started) >= expectedCalls)
            {
                _allStarted.TrySetResult();
            }

            using var registration = cancellationToken.Register(() => _release.TrySetCanceled(cancellationToken));
            await _release.Task.WaitAsync(cancellationToken);

            var fileName = $"notes-{request.ModelOverride}.md";
            File.WriteAllText(
                Path.Combine(request.WorkingDirectory, fileName),
                $"Model {request.ModelOverride} completed arena task.");

            return new AssistantTurnResponse
            {
                Summary = $"{request.ModelOverride} completed arena task.",
                ProviderName = "fake-arena",
                Model = request.ModelOverride ?? string.Empty,
                ToolExecutions = []
            };
        }

        public Task WaitUntilStartedAsync() => _allStarted.Task;

        public void Release() => _release.TrySetResult();
    }

    private sealed class ApplyingArenaTurnRuntime : IAssistantTurnRuntime
    {
        public Task<AssistantTurnResponse> GenerateAsync(
            AssistantTurnRequest request,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default)
        {
            var model = request.ModelOverride ?? string.Empty;
            File.WriteAllText(
                Path.Combine(request.WorkingDirectory, "README.md"),
                $"{model} applied change");
            File.WriteAllText(
                Path.Combine(request.WorkingDirectory, $"notes-{model}.md"),
                $"Notes from {model}");

            return Task.FromResult(new AssistantTurnResponse
            {
                Summary = $"{model} completed arena task.",
                ProviderName = "fake-arena",
                Model = model,
                ToolExecutions = []
            });
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition(), "Condition was not satisfied before timeout.");
    }

    private sealed class StaticServiceProvider(IAssistantTurnRuntime runtime) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IAssistantTurnRuntime) ? runtime : null;
    }
}
