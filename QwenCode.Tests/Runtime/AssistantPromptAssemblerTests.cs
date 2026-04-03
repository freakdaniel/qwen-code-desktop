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
}
