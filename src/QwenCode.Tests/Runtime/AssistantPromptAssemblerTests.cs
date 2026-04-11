using QwenCode.Core.Mcp;
using QwenCode.Core.Prompts;

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

                ## Qwen Added Memories
                - Persist renderer reconnect behavior.
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

            var assembler = new AssistantPromptAssembler(
                new ProjectSummaryService(),
                null,
                new FakeMcpConnectionManager(
                    new McpServerDefinition
                    {
                        Name = "docs",
                        Scope = "project",
                        Transport = "http",
                        CommandOrUrl = "http://docs",
                        Status = "connected",
                        Instructions = "Use this server for repo-specific docs and prompts.",
                        DiscoveredToolsCount = 3,
                        DiscoveredPromptsCount = 2,
                        SupportsPrompts = true,
                        SupportsResources = true
                    }),
                new FakePromptRegistryService(
                    new PromptRegistrySnapshot
                    {
                        TotalCount = 2,
                        ServerCount = 1,
                        Prompts =
                        [
                            new PromptRegistryEntry
                            {
                                Name = "workspace-summary",
                                PromptName = "workspace-summary",
                                QualifiedName = "docs/workspace-summary",
                                ServerName = "docs",
                                Description = "Summarize the workspace using repository-specific knowledge.",
                                ArgumentsJson = """[{"name":"scope"},{"name":"format"}]"""
                            },
                            new PromptRegistryEntry
                            {
                                Name = "release-notes",
                                PromptName = "release-notes",
                                QualifiedName = "docs/release-notes",
                                ServerName = "docs",
                                Description = "Read the release notes for a given version.",
                                ArgumentsJson = """[{"name":"version"}]"""
                            }
                        ]
                    }));
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
                        CurrentLocale = "en",
                        CurrentLanguage = "English",
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
            Assert.Contains("Workspace root:", promptContext.EnvironmentSummary);
            Assert.Contains("Platform:", promptContext.EnvironmentSummary);
            Assert.Contains("Shell:", promptContext.EnvironmentSummary);
            Assert.Contains("Runtime base directory:", promptContext.EnvironmentSummary);
            Assert.Contains("Workspace trusted:", promptContext.EnvironmentSummary);
            Assert.Contains("Transcript messages retained for this turn:", promptContext.SessionGuidanceSummary);
            Assert.Contains("Project summary available", promptContext.SessionGuidanceSummary);
            Assert.Contains("Prefer native runtime integrations.", promptContext.UserInstructionSummary);
            Assert.Contains("desktop runtime must stay native", promptContext.WorkspaceInstructionSummary);
            Assert.Contains("Respect repository conventions.", promptContext.WorkspaceInstructionSummary);
            Assert.Contains("reconnect flow must survive renderer reloads", promptContext.WorkspaceInstructionSummary);
            Assert.Contains("Project durable memory", promptContext.DurableMemorySummary);
            Assert.Contains("Persist renderer reconnect behavior.", promptContext.DurableMemorySummary);
            Assert.Contains("docs (project, http): 3 tool(s), 2 prompt(s), resources available", promptContext.McpServerSummary);
            Assert.Contains("prompts available", promptContext.McpServerSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("repo-specific docs and prompts", promptContext.McpServerSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Use `mcp-client` with `server_name` + `prompt_name`", promptContext.McpServerSummary, StringComparison.Ordinal);
            Assert.Contains("Use `mcp-tool` for concrete server-exposed actions", promptContext.McpServerSummary, StringComparison.Ordinal);
            Assert.Contains("Discovered MCP prompts: 2 across 1 server(s).", promptContext.McpPromptRegistrySummary);
            Assert.Contains("`docs/workspace-summary`", promptContext.McpPromptRegistrySummary, StringComparison.Ordinal);
            Assert.Contains("Args: scope, format.", promptContext.McpPromptRegistrySummary, StringComparison.Ordinal);
            Assert.Contains("scratchpad", promptContext.ScratchpadSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("session-1", promptContext.ScratchpadSummary, StringComparison.Ordinal);
            Assert.Contains("Preferred locale: en", promptContext.LanguageSummary);
            Assert.Contains("Preferred language: English", promptContext.LanguageSummary);
            Assert.Contains("Mode-specific expectation:", promptContext.OutputStyleSummary);
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
                        CurrentLocale = "en",
                        CurrentLanguage = "English",
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
            Assert.Contains("Workspace trusted:", promptContext.EnvironmentSummary);
            Assert.Contains("Platform:", promptContext.EnvironmentSummary);
            Assert.Contains("No project summary is available.", promptContext.SessionGuidanceSummary);
            Assert.Contains("Only global context should load.", promptContext.UserInstructionSummary);
            Assert.True(string.IsNullOrWhiteSpace(promptContext.WorkspaceInstructionSummary));
            Assert.True(string.IsNullOrWhiteSpace(promptContext.DurableMemorySummary));
            Assert.True(string.IsNullOrWhiteSpace(promptContext.McpServerSummary));
            Assert.Contains("scratchpad", promptContext.ScratchpadSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Preferred language: English", promptContext.LanguageSummary);
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
            Assert.Contains("Transcript messages retained for this turn: 3", promptContext.SessionGuidanceSummary);
            Assert.Contains("Compression checkpoint: summarized earlier transcript entries.", promptContext.SessionMemorySummary);
            Assert.Contains("Session memory checkpoint retained", promptContext.SessionGuidanceSummary);
            Assert.True(string.IsNullOrWhiteSpace(promptContext.UserInstructionSummary));
            Assert.True(string.IsNullOrWhiteSpace(promptContext.WorkspaceInstructionSummary));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantPromptAssembler_AssembleAsync_RetainsCompressionCheckpointAsSessionMemoryWhenHistoryIsTrimmed()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-session-memory-{Guid.NewGuid():N}");
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

            var transcriptPath = Path.Combine(chatsRoot, "session-memory.jsonl");
            var lines = new List<string>
            {
                """{"uuid":"1","sessionId":"session-memory","timestamp":"2026-04-03T10:00:00Z","type":"user","message":{"parts":[{"text":"Very old request before compaction"}]}}""",
                """{"uuid":"2","sessionId":"session-memory","timestamp":"2026-04-03T10:05:00Z","type":"system","status":"chat-compression","messageText":"Compression checkpoint: preserve the direct-connect SSE contract and approval fix context."}"""
            };
            lines.AddRange(
                Enumerable.Range(3, 24)
                    .Select(index => $"{{\"uuid\":\"{index}\",\"sessionId\":\"session-memory\",\"timestamp\":\"2026-04-03T10:{index:00}:00Z\",\"type\":\"user\",\"message\":{{\"parts\":[{{\"text\":\"Recent request {index} {new string('x', 180)}\"}}]}}}}"));
            File.WriteAllLines(transcriptPath, lines);

            var assembler = new AssistantPromptAssembler(new ProjectSummaryService());
            var promptContext = await assembler.AssembleAsync(
                new AssistantTurnRequest
                {
                    SessionId = "session-memory",
                    Prompt = "Continue with tiny context",
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
                        CurrentLocale = "en",
                        CurrentLanguage = "English",
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

            Assert.True(promptContext.TrimmedTranscriptMessageCount > 0);
            Assert.DoesNotContain(promptContext.Messages, message => message.Content.Contains("direct-connect SSE contract", StringComparison.Ordinal));
            Assert.Contains("Recorded at 2026-04-03T10:05:00Z", promptContext.SessionMemorySummary);
            Assert.Contains("direct-connect SSE contract", promptContext.SessionMemorySummary);
            Assert.Contains("Session memory checkpoint retained", promptContext.SessionGuidanceSummary);
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

            var assembler = new AssistantPromptAssembler(
                new ProjectSummaryService(),
                null,
                new FakeMcpConnectionManager(
                    new McpServerDefinition
                    {
                        Name = "docs",
                        Scope = "project",
                        Transport = "http",
                        CommandOrUrl = "http://docs",
                        Status = "connected",
                        DiscoveredToolsCount = 3,
                        DiscoveredPromptsCount = 2,
                        SupportsResources = true
                    }));
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
                        CurrentLocale = "en",
                        CurrentLanguage = "English",
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
            Assert.Contains("Budget trimmed transcript messages:", promptContext.SessionGuidanceSummary);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeMcpConnectionManager(params McpServerDefinition[] servers) : IMcpConnectionManager
    {
        private readonly IReadOnlyList<McpServerDefinition> configuredServers = servers;

        public IReadOnlyList<McpServerDefinition> ListServersWithStatus(WorkspacePaths paths) => configuredServers;

        public Task<McpReconnectResult> ReconnectAsync(
            WorkspacePaths paths,
            string name,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new McpReconnectResult
            {
                Name = name,
                Status = "connected",
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                Message = "reconnected"
            });

        public Task DisconnectAsync(
            WorkspacePaths paths,
            string name,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePromptRegistryService(PromptRegistrySnapshot snapshot) : IPromptRegistryService
    {
        public Task<PromptRegistrySnapshot> GetSnapshotAsync(
            WorkspacePaths paths,
            GetPromptRegistryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);

        public Task<McpPromptInvocationResult> InvokeAsync(
            WorkspacePaths paths,
            InvokePromptRegistryEntryRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
