namespace QwenCode.Tests.Runtime;

public sealed class AssistantPromptAssemblerTests
{
    [Fact]
    public async Task AssistantPromptAssembler_AssemblesTranscriptAndContextFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var runtimeRoot = Path.Combine(root, "runtime");
            var homeRoot = Path.Combine(root, "home");
            var chatsRoot = Path.Combine(runtimeRoot, "projects", "project-a", "chats");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(chatsRoot);

            var nestedWorkingDirectory = Path.Combine(workspaceRoot, "src", "feature");
            Directory.CreateDirectory(nestedWorkingDirectory);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));

            var transcriptPath = Path.Combine(chatsRoot, "session-1.jsonl");
            File.WriteAllLines(
                transcriptPath,
                [
                    """{"uuid":"1","sessionId":"session-1","type":"user","message":{"parts":[{"text":"Review the service layer."}]}}""",
                    """{"uuid":"2","sessionId":"session-1","type":"command","commandName":"context","status":"completed","resolvedPrompt":"Show context"}""",
                    """{"uuid":"3","sessionId":"session-1","type":"system","status":"interrupted","messageText":"The previous desktop turn was interrupted."}""",
                    """{"uuid":"4","sessionId":"session-1","type":"assistant","message":{"parts":[{"text":"Context loaded."}]}}"""
                ]);

            var globalContextPath = Path.Combine(homeRoot, ".qwen", "QWEN.md");
            Directory.CreateDirectory(Path.GetDirectoryName(globalContextPath)!);
            File.WriteAllText(globalContextPath, "# Global memory\nPrefer native runtime integrations.");

            var importedContextPath = Path.Combine(workspaceRoot, "docs", "guidelines.md");
            Directory.CreateDirectory(Path.GetDirectoryName(importedContextPath)!);
            File.WriteAllText(importedContextPath, "Respect repository conventions.");

            var rootContextPath = Path.Combine(workspaceRoot, "QWEN.md");
            File.WriteAllText(
                rootContextPath,
                """
                # Project memory
                Remember the desktop runtime must stay native.
                @docs/guidelines.md
                """
            );

            var nestedContextPath = Path.Combine(nestedWorkingDirectory, "AGENTS.md");
            File.WriteAllText(nestedContextPath, "# Feature memory\nThe reconnect flow must survive renderer reloads.");

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "PROJECT_SUMMARY.md"),
                """
                **Update time**: 2026-04-02T09:00:00Z

                ## Overall Goal
                Finish the native qwen desktop runtime.

                ## Current Plan
                1. [DONE] Refactor the app host.
                2. [IN PROGRESS] Implement reconnect.
                3. [TODO] Parse project summary in C#.
                4. [TODO] Surface welcome-back context.
                """
            );

            var assembler = new AssistantPromptAssembler(new ProjectSummaryService());
            var promptContext = await assembler.AssembleAsync(
                new AssistantTurnRequest
                {
                    SessionId = "session-1",
                    Prompt = "Continue the refactor.",
                    WorkingDirectory = nestedWorkingDirectory,
                    TranscriptPath = transcriptPath,
                    RuntimeProfile = new QwenRuntimeProfile
                    {
                        ProjectRoot = workspaceRoot,
                        GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
                        RuntimeBaseDirectory = runtimeRoot,
                        RuntimeSource = "test",
                        ProjectDataDirectory = Path.Combine(runtimeRoot, "projects", "project-a"),
                        ChatsDirectory = chatsRoot,
                        HistoryDirectory = Path.Combine(runtimeRoot, "history", "project-a"),
                        ContextFileNames = ["QWEN.md", "AGENTS.md"],
                        ContextFilePaths = [rootContextPath, nestedContextPath],
                        FolderTrustEnabled = true,
                        IsWorkspaceTrusted = true,
                        WorkspaceTrustSource = "file",
                        ApprovalProfile = new ApprovalProfile
                        {
                            DefaultMode = "default",
                            ConfirmShellCommands = true,
                            ConfirmFileEdits = true,
                            AllowRules = [],
                            AskRules = [],
                            DenyRules = []
                        }
                    },
                    GitBranch = "main",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = string.Empty,
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    }
                });

            Assert.Equal(4, promptContext.Messages.Count);
            Assert.Equal(3, promptContext.ContextFiles.Count);
            Assert.Contains("Global memory", promptContext.ContextFiles[0]);
            Assert.Contains("desktop runtime must stay native", promptContext.ContextFiles[1]);
            Assert.Contains("Respect repository conventions.", promptContext.ContextFiles[1]);
            Assert.Contains("reconnect flow must survive renderer reloads", promptContext.ContextFiles[2]);
            Assert.Contains("Transcript messages loaded: 4", promptContext.SessionSummary);
            Assert.Contains("Context file names: QWEN.md, AGENTS.md", promptContext.SessionSummary);
            Assert.Contains("Project summary: loaded", promptContext.SessionSummary);
            Assert.NotNull(promptContext.ProjectSummary);
            Assert.True(promptContext.ProjectSummary!.HasHistory);
            Assert.Equal("Finish the native qwen desktop runtime.", promptContext.ProjectSummary.OverallGoal);
            Assert.Equal(4, promptContext.ProjectSummary.TotalTasks);
            Assert.Equal(3, promptContext.ProjectSummary.PendingTasks.Count);
            Assert.Contains(promptContext.HistoryHighlights, item => item.Contains("Review the service layer.", StringComparison.Ordinal));
            Assert.Contains(promptContext.HistoryHighlights, item => item.Contains("interrupted", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantPromptAssembler_AssembleAsync_UntrustedWorkspace_LoadsOnlyGlobalContext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-context-untrusted-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var runtimeRoot = Path.Combine(root, "runtime");
            var homeRoot = Path.Combine(root, "home");
            var chatsRoot = Path.Combine(runtimeRoot, "projects", "project-a", "chats");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(chatsRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));

            var nestedWorkingDirectory = Path.Combine(workspaceRoot, "src", "feature");
            Directory.CreateDirectory(nestedWorkingDirectory);

            var transcriptPath = Path.Combine(chatsRoot, "session-1.jsonl");
            File.WriteAllText(
                transcriptPath,
                """{"uuid":"1","sessionId":"session-1","type":"user","message":{"parts":[{"text":"Continue the work."}]}}""");

            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "QWEN.md"),
                "# Global memory\nOnly global context should load.");
            File.WriteAllText(
                Path.Combine(workspaceRoot, "QWEN.md"),
                "# Project memory\nThis must stay hidden.");
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "PROJECT_SUMMARY.md"),
                """
                ## Overall Goal
                Hidden in untrusted workspace.
                """);

            var assembler = new AssistantPromptAssembler(new ProjectSummaryService());
            var promptContext = await assembler.AssembleAsync(
                new AssistantTurnRequest
                {
                    SessionId = "session-1",
                    Prompt = "Continue the refactor.",
                    WorkingDirectory = nestedWorkingDirectory,
                    TranscriptPath = transcriptPath,
                    RuntimeProfile = new QwenRuntimeProfile
                    {
                        ProjectRoot = workspaceRoot,
                        GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
                        RuntimeBaseDirectory = runtimeRoot,
                        RuntimeSource = "test",
                        ProjectDataDirectory = Path.Combine(runtimeRoot, "projects", "project-a"),
                        ChatsDirectory = chatsRoot,
                        HistoryDirectory = Path.Combine(runtimeRoot, "history", "project-a"),
                        ContextFileNames = ["QWEN.md", "AGENTS.md"],
                        ContextFilePaths = [],
                        FolderTrustEnabled = true,
                        IsWorkspaceTrusted = false,
                        WorkspaceTrustSource = "file",
                        ApprovalProfile = new ApprovalProfile
                        {
                            DefaultMode = "default",
                            ConfirmShellCommands = true,
                            ConfirmFileEdits = true,
                            AllowRules = [],
                            AskRules = [],
                            DenyRules = []
                        }
                    },
                    GitBranch = "main",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = string.Empty,
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    }
                });

            Assert.Single(promptContext.ContextFiles);
            Assert.Contains("Only global context should load.", promptContext.ContextFiles[0]);
            Assert.DoesNotContain("Project memory", promptContext.ContextFiles[0], StringComparison.Ordinal);
            Assert.Null(promptContext.ProjectSummary);
            Assert.Contains("Project summary: not found.", promptContext.SessionSummary);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantPromptAssembler_AssembleAsync_UsesSessionServiceCompressionAwareHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-compressed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var runtimeRoot = Path.Combine(root, "runtime");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var chatsRoot = runtimeProfile.ChatsDirectory;
            Directory.CreateDirectory(chatsRoot);

            var transcriptPath = Path.Combine(chatsRoot, "session-2.jsonl");
            File.WriteAllLines(
                transcriptPath,
                [
                    """{"uuid":"1","sessionId":"session-2","timestamp":"2026-04-03T10:00:00Z","type":"user","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"parts":[{"text":"Older request"}]}}""",
                    """{"uuid":"2","sessionId":"session-2","timestamp":"2026-04-03T10:02:00Z","type":"assistant","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"parts":[{"text":"Older answer"}]}}""",
                    """{"uuid":"3","sessionId":"session-2","timestamp":"2026-04-03T10:05:00Z","type":"system","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","status":"chat-compression","messageText":"Compression checkpoint: summarized earlier transcript entries."}""",
                    """{"uuid":"4","sessionId":"session-2","timestamp":"2026-04-03T10:06:00Z","type":"user","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"parts":[{"text":"Most recent user request"}]}}""",
                    """{"uuid":"5","sessionId":"session-2","timestamp":"2026-04-03T10:07:00Z","type":"assistant","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"parts":[{"text":"Most recent assistant answer"}]}}"""
                ]);

            var sessionService = new DesktopSessionCatalogService(
                runtimeProfileService,
                new ChatRecordingService());
            var assembler = new AssistantPromptAssembler(new ProjectSummaryService(), sessionService);

            var promptContext = await assembler.AssembleAsync(
                new AssistantTurnRequest
                {
                    SessionId = "session-2",
                    Prompt = "Resume work",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = transcriptPath,
                    RuntimeProfile = runtimeProfile,
                    GitBranch = "main",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = string.Empty,
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    }
                });

            Assert.Equal(3, promptContext.Messages.Count);
            Assert.Equal("system", promptContext.Messages[0].Role);
            Assert.Contains("Compression checkpoint", promptContext.Messages[0].Content);
            Assert.Equal("user", promptContext.Messages[1].Role);
            Assert.Contains("Most recent user request", promptContext.Messages[1].Content);
            Assert.Equal("assistant", promptContext.Messages[2].Role);
            Assert.Contains("Most recent assistant answer", promptContext.Messages[2].Content);
            Assert.Contains("Transcript messages loaded: 3", promptContext.SessionSummary);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantPromptAssembler_AssembleAsync_TrimsContextToFitResolvedInputBudget()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-budget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var runtimeRoot = Path.Combine(root, "runtime");
            var homeRoot = Path.Combine(root, "home");
            var chatsRoot = Path.Combine(runtimeRoot, "projects", "project-a", "chats");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(chatsRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));

            var transcriptPath = Path.Combine(chatsRoot, "session-budget.jsonl");
            File.WriteAllLines(
                transcriptPath,
                Enumerable.Range(1, 12)
                    .Select(index => $"{{\"uuid\":\"{index}\",\"sessionId\":\"session-budget\",\"type\":\"user\",\"message\":{{\"parts\":[{{\"text\":\"{new string('x', 220)} {index}\"}}]}}}}"));

            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "QWEN.md"),
                new string('g', 2400));
            File.WriteAllText(
                Path.Combine(workspaceRoot, "QWEN.md"),
                new string('p', 2400));

            var assembler = new AssistantPromptAssembler(new ProjectSummaryService());
            var promptContext = await assembler.AssembleAsync(
                new AssistantTurnRequest
                {
                    SessionId = "session-budget",
                    Prompt = "Budget sensitive prompt",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = transcriptPath,
                    RuntimeProfile = new QwenRuntimeProfile
                    {
                        ProjectRoot = workspaceRoot,
                        GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
                        RuntimeBaseDirectory = runtimeRoot,
                        RuntimeSource = "test",
                        ProjectDataDirectory = Path.Combine(runtimeRoot, "projects", "project-a"),
                        ChatsDirectory = chatsRoot,
                        HistoryDirectory = Path.Combine(runtimeRoot, "history", "project-a"),
                        ContextFileNames = ["QWEN.md"],
                        ContextFilePaths = [],
                        FolderTrustEnabled = true,
                        IsWorkspaceTrusted = true,
                        WorkspaceTrustSource = "file",
                        ApprovalProfile = new ApprovalProfile
                        {
                            DefaultMode = "default",
                            ConfirmShellCommands = true,
                            ConfirmFileEdits = true,
                            AllowRules = [],
                            AskRules = [],
                            DenyRules = []
                        }
                    },
                    GitBranch = "main",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = string.Empty,
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    }
                },
                new ResolvedTokenLimits
                {
                    Model = "tiny-budget-model",
                    NormalizedModel = "tiny-budget-model",
                    InputTokenLimit = 1024,
                    OutputTokenLimit = 512,
                    HasExplicitOutputLimit = true
                });

            Assert.True(promptContext.WasBudgetTrimmed);
            Assert.True(promptContext.TrimmedTranscriptMessageCount > 0 || promptContext.TrimmedContextFileCount > 0);
            Assert.True(promptContext.Messages.Count < 12 || promptContext.ContextFiles.Count < 2);
            Assert.Contains("Prompt budget trimmed: True", promptContext.SessionSummary);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
