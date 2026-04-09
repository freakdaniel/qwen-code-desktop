using System.Text.Json.Nodes;

namespace QwenCode.Tests.Runtime;

public sealed class DesktopPromptAndToolLoopTests
{
    [Fact]
    public void OpenAiCompatibleProtocol_BuildPayload_UsesAgenticCurrentTurnPrompt()
    {
        var request = CreateTurnRequest();
        var promptContext = new AssistantPromptContext
        {
            SessionSummary = "Transcript messages loaded: 2",
            EnvironmentSummary = "Workspace root: D:\\repo",
            SessionGuidanceSummary = "Transcript messages retained for this turn: 2",
            UserInstructionSummary = "From ~/.qwen/QWEN.md:\n- Prefer native runtime integrations.",
            WorkspaceInstructionSummary = "From QWEN.md:\n- Keep reconnect flow stable.",
            DurableMemorySummary = "Project durable memory (QWEN.md):\n- Remember reconnect state.",
            McpServerSummary = "- docs (project): 3 tool(s), 2 prompt(s), resources available",
            McpPromptRegistrySummary = "Discovered MCP prompts: 2 across 1 server(s).\n- `docs/workspace-summary`. Summarizes the workspace. Args: scope.\n- `docs/release-notes`. Reads release notes. Args: version.",
            ScratchpadSummary = "Use `D:\\runtime\\tmp\\scratchpad\\desktop-prompt-session` for temporary files.",
            LanguageSummary = "Preferred locale: en\nPreferred language: English",
            OutputStyleSummary = "Mode-specific expectation: Prefer concise, action-oriented answers.",
            HistoryHighlights = ["user: fix the web tools", "assistant: investigating runtime behavior"],
            ContextFiles = ["--- Context from: AGENTS.md ---\nUse the tools carefully.\n--- End of Context from: AGENTS.md ---"],
            Messages =
            [
                new AssistantConversationMessage
                {
                    Role = "user",
                    Content = "Previous user turn"
                }
            ]
        };

        var payload = OpenAiCompatibleProtocol.BuildPayload(
            "qwen3-coder-plus",
            0.2d,
            4096,
            NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt,
            request,
            promptContext,
            [],
            null,
            null);

        var messages = Assert.IsType<JsonArray>(payload["messages"]);
        var systemMessage = Assert.IsType<JsonObject>(messages[0]);
        var systemContent = systemMessage["content"]?.GetValue<string>() ?? string.Empty;
        Assert.Contains("agentic coding assistant", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("# Identity", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Task Management", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Tool Loop Rules", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Tool Result Persistence", systemContent, StringComparison.Ordinal);
        Assert.Contains("# System Reminders", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Tool Call Format", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Tool Call Examples", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Using Tools", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Context And Memory Management", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Long-Session Maintenance", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Memory Hygiene", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Advanced Tool Workflows", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Research And Uncertainty", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Web Research Workflow", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Available Tools This Turn", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Tool Playbooks", systemContent, StringComparison.Ordinal);
        Assert.Contains("# MCP Server Instructions", systemContent, StringComparison.Ordinal);
        Assert.Contains("# MCP Prompt Registry", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Delegation And Handoffs", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Scratchpad Directory", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Environment", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Session Guidance", systemContent, StringComparison.Ordinal);
        Assert.Contains("# User Instructions", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Workspace Instructions", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Durable Memory", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Language Preferences", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Output Expectations", systemContent, StringComparison.Ordinal);
        Assert.Contains("OpenAI-compatible function calling", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("docs (project): 3 tool(s), 2 prompt(s), resources available", systemContent, StringComparison.Ordinal);
        Assert.Contains("Discovered MCP prompts: 2 across 1 server(s).", systemContent, StringComparison.Ordinal);
        Assert.Contains("scratchpad", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("old raw tool output", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("conversation may be summarized or compressed automatically", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("include the current year", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compact working memory", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("high-signal store", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("todo_write", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("task_create", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("task_update", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("run_shell_command", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("web_search", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tool_search", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Use `lsp` for semantic code intelligence", systemContent, StringComparison.Ordinal);
        Assert.Contains("Use `mcp-client` to inspect connected MCP servers", systemContent, StringComparison.Ordinal);
        Assert.Contains("Use `arena` for compare-and-choose tasks", systemContent, StringComparison.Ordinal);
        Assert.Contains("use `tool_search` to discover the most relevant tools", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not delegate understanding", systemContent, StringComparison.Ordinal);
        Assert.Contains("## `web_search`", systemContent, StringComparison.Ordinal);
        Assert.Contains("## `agent`", systemContent, StringComparison.Ordinal);
        Assert.Contains("## `mcp-client`", systemContent, StringComparison.Ordinal);

        var userMessage = Assert.IsType<JsonObject>(messages[2]);
        var userContent = userMessage["content"]?.GetValue<string>() ?? string.Empty;
        Assert.Contains("Current user task:", userContent, StringComparison.Ordinal);
        Assert.Contains("Turn constraints:", userContent, StringComparison.Ordinal);
        Assert.Contains("Tool loop guidance:", userContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Write a concise desktop assistant response", userContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenAiCompatibleProtocol_BuildPayload_AddsApprovalResolutionOverlayWhenNeeded()
    {
        var baseRequest = CreateTurnRequest();
        var request = new AssistantTurnRequest
        {
            SessionId = baseRequest.SessionId,
            Prompt = baseRequest.Prompt,
            WorkingDirectory = baseRequest.WorkingDirectory,
            TranscriptPath = baseRequest.TranscriptPath,
            RuntimeProfile = baseRequest.RuntimeProfile,
            GitBranch = baseRequest.GitBranch,
            ToolExecution = baseRequest.ToolExecution,
            IsApprovalResolution = true
        };

        var payload = OpenAiCompatibleProtocol.BuildPayload(
            "qwen3-coder-plus",
            0.2d,
            4096,
            NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt,
            request,
            CreatePromptContext(),
            [],
            null,
            null);

        var messages = Assert.IsType<JsonArray>(payload["messages"]);
        var systemMessage = Assert.IsType<JsonObject>(messages[0]);
        var systemContent = systemMessage["content"]?.GetValue<string>() ?? string.Empty;
        Assert.Contains("# Approval Resolution", systemContent, StringComparison.Ordinal);
        Assert.Contains("approval, denial, or permission-related interruption", systemContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenAiCompatibleProtocol_BuildPayload_AddsToolAvailabilityOverlayWhenToolsAreRestricted()
    {
        var baseRequest = CreateTurnRequest();
        var request = new AssistantTurnRequest
        {
            SessionId = baseRequest.SessionId,
            Prompt = baseRequest.Prompt,
            WorkingDirectory = baseRequest.WorkingDirectory,
            TranscriptPath = baseRequest.TranscriptPath,
            RuntimeProfile = baseRequest.RuntimeProfile,
            GitBranch = baseRequest.GitBranch,
            ToolExecution = baseRequest.ToolExecution,
            AllowedToolNames = ["web_search", "web_fetch"]
        };

        var payload = OpenAiCompatibleProtocol.BuildPayload(
            "qwen3-coder-plus",
            0.2d,
            4096,
            NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt,
            request,
            CreatePromptContext(),
            [],
            null,
            null);

        var messages = Assert.IsType<JsonArray>(payload["messages"]);
        var systemMessage = Assert.IsType<JsonObject>(messages[0]);
        var systemContent = systemMessage["content"]?.GetValue<string>() ?? string.Empty;
        Assert.Contains("# Tool Availability", systemContent, StringComparison.Ordinal);
        Assert.Contains("web_search, web_fetch", systemContent, StringComparison.Ordinal);
        Assert.Contains("# Tool Playbooks", systemContent, StringComparison.Ordinal);
        Assert.Contains("## `web_search`", systemContent, StringComparison.Ordinal);
        Assert.Contains("## `web_fetch`", systemContent, StringComparison.Ordinal);
        Assert.DoesNotContain("## `agent`", systemContent, StringComparison.Ordinal);
        Assert.DoesNotContain("todo_write", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("save_memory", systemContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenAiCompatibleProtocol_BuildPayload_ExposesDetailedSchemasForAgenticTools()
    {
        var payload = OpenAiCompatibleProtocol.BuildPayload(
            "qwen3-coder-plus",
            0.2d,
            4096,
            NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt,
            CreateTurnRequest(),
            CreatePromptContext(),
            [],
            null,
            null);

        var tools = Assert.IsType<JsonArray>(payload["tools"]);
        var toolsByName = tools
            .OfType<JsonObject>()
            .ToDictionary(
                static item => item["function"]?["name"]?.GetValue<string>() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var webSearch = toolsByName["web_search"];
        Assert.Contains("facts may have changed", webSearch["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("query", Assert.IsType<JsonArray>(webSearch["function"]?["parameters"]?["required"])[0]?.GetValue<string>());
        Assert.Contains("Include a concrete year", webSearch["function"]?["parameters"]?["properties"]?["query"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var todoWrite = toolsByName["todo_write"];
        Assert.Contains("update it as you work", todoWrite["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("array", todoWrite["function"]?["parameters"]?["properties"]?["todos"]?["type"]?.GetValue<string>());

        var askUser = toolsByName["ask_user_question"];
        Assert.Equal("questions", Assert.IsType<JsonArray>(askUser["function"]?["parameters"]?["required"])[0]?.GetValue<string>());
        Assert.Equal("array", askUser["function"]?["parameters"]?["properties"]?["questions"]?["type"]?.GetValue<string>());

        var taskCreate = toolsByName["task_create"];
        Assert.Contains("ownership", taskCreate["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        var taskCreateRequired = Assert.IsType<JsonArray>(taskCreate["function"]?["parameters"]?["required"]);
        Assert.Contains(taskCreateRequired, static item => string.Equals(item?.GetValue<string>(), "subject", StringComparison.Ordinal));
        Assert.Contains(taskCreateRequired, static item => string.Equals(item?.GetValue<string>(), "description", StringComparison.Ordinal));

        var taskUpdate = toolsByName["task_update"];
        Assert.Equal("task_id", Assert.IsType<JsonArray>(taskUpdate["function"]?["parameters"]?["required"])[0]?.GetValue<string>());

        var taskList = toolsByName["task_list"];
        Assert.Contains("orchestration tasks", taskList["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var agent = toolsByName["agent"];
        Assert.Contains("final synthesis", agent["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        var agentRequired = Assert.IsType<JsonArray>(agent["function"]?["parameters"]?["required"]);
        Assert.Contains(agentRequired, static item => string.Equals(item?.GetValue<string>(), "subagent_type", StringComparison.Ordinal));
        Assert.Contains("goal, relevant files, constraints", agent["function"]?["parameters"]?["properties"]?["prompt"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var arena = toolsByName["arena"];
        Assert.Contains("arena session", arena["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("array", arena["function"]?["parameters"]?["properties"]?["models"]?["type"]?.GetValue<string>());

        var mcpClient = toolsByName["mcp-client"];
        Assert.Contains("MCP server", mcpClient["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("object", mcpClient["function"]?["parameters"]?["properties"]?["arguments"]?["type"]?.GetValue<string>());

        var mcpTool = toolsByName["mcp-tool"];
        var mcpToolRequired = Assert.IsType<JsonArray>(mcpTool["function"]?["parameters"]?["required"]);
        Assert.Contains(mcpToolRequired, static item => string.Equals(item?.GetValue<string>(), "server_name", StringComparison.Ordinal));
        Assert.Contains(mcpToolRequired, static item => string.Equals(item?.GetValue<string>(), "tool_name", StringComparison.Ordinal));

        var lsp = toolsByName["lsp"];
        Assert.Contains("Roslyn code intelligence", lsp["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("operation", Assert.IsType<JsonArray>(lsp["function"]?["parameters"]?["required"])[0]?.GetValue<string>());
        Assert.Equal("string", lsp["function"]?["parameters"]?["properties"]?["operation"]?["type"]?.GetValue<string>());

        var toolSearch = toolsByName["tool_search"];
        Assert.Contains("before guessing", toolSearch["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("string", toolSearch["function"]?["parameters"]?["properties"]?["query"]?["type"]?.GetValue<string>());
        Assert.Equal("string", toolSearch["function"]?["parameters"]?["properties"]?["kind"]?["type"]?.GetValue<string>());

        var webFetch = toolsByName["web_fetch"];
        Assert.Contains("likely source", webFetch["function"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("specific facts", webFetch["function"]?["parameters"]?["properties"]?["prompt"]?["description"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AssistantPromptMode.Plan, "# Plan Mode")]
    [InlineData(AssistantPromptMode.FollowupSuggestion, "# Follow-Up Suggestion Mode")]
    [InlineData(AssistantPromptMode.Subagent, "# Subagent Mode")]
    [InlineData(AssistantPromptMode.ArenaCompetitor, "# Arena Competitor Mode")]
    public void OpenAiCompatibleProtocol_BuildPayload_AddsPromptModeOverlay(
        AssistantPromptMode promptMode,
        string expectedSection)
    {
        var baseRequest = CreateTurnRequest();
        var request = new AssistantTurnRequest
        {
            SessionId = baseRequest.SessionId,
            Prompt = baseRequest.Prompt,
            WorkingDirectory = baseRequest.WorkingDirectory,
            TranscriptPath = baseRequest.TranscriptPath,
            RuntimeProfile = baseRequest.RuntimeProfile,
            GitBranch = baseRequest.GitBranch,
            ToolExecution = baseRequest.ToolExecution,
            PromptMode = promptMode
        };

        var payload = OpenAiCompatibleProtocol.BuildPayload(
            "qwen3-coder-plus",
            0.2d,
            4096,
            NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt,
            request,
            CreatePromptContext(),
            [],
            null,
            null);

        var messages = Assert.IsType<JsonArray>(payload["messages"]);
        var systemMessage = Assert.IsType<JsonObject>(messages[0]);
        var systemContent = systemMessage["content"]?.GetValue<string>() ?? string.Empty;
        Assert.Contains(expectedSection, systemContent, StringComparison.Ordinal);

        if (promptMode == AssistantPromptMode.Plan)
        {
            Assert.Contains("read-only investigation and planning", systemContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Do not edit files, write files, create commits", systemContent, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OpenAiCompatibleProtocol_BuildPayload_UsesQwenSpecificToolCallGuidanceForDashScope()
    {
        var payload = OpenAiCompatibleProtocol.BuildPayload(
            "qwen3-coder-plus",
            0.2d,
            4096,
            NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt,
            CreateTurnRequest(),
            CreatePromptContext(),
            [],
            null,
            null,
            "dashscope");

        var messages = Assert.IsType<JsonArray>(payload["messages"]);
        var systemMessage = Assert.IsType<JsonObject>(messages[0]);
        var systemContent = systemMessage["content"]?.GetValue<string>() ?? string.Empty;
        Assert.Contains("qwen-compatible function calling", systemContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider flavor `dashscope`", systemContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeAssistantRuntimePromptBuilder_StaticAndDynamicSections_AreSplitDeterministically()
    {
        var request = CreateTurnRequest();
        var promptContext = CreatePromptContext();
        var compositionContext = new NativeAssistantPromptCompositionContext(
            request,
            promptContext,
            "Base runtime instructions.",
            "Request-specific instructions.",
            "qwen3-coder-plus",
            "dashscope");

        var staticPrefix = NativeAssistantRuntimePromptBuilder.BuildStaticSystemPromptPrefix(compositionContext);
        var dynamicTail = NativeAssistantRuntimePromptBuilder.BuildDynamicSystemPromptTail(compositionContext);
        var fullPrompt = NativeAssistantRuntimePromptBuilder.BuildSystemPrompt(
            request,
            promptContext,
            "Base runtime instructions.",
            "Request-specific instructions.",
            "qwen3-coder-plus",
            "dashscope");

        Assert.Contains("# Identity", staticPrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("# Environment", staticPrefix, StringComparison.Ordinal);
        Assert.DoesNotContain("# Runtime Instructions", staticPrefix, StringComparison.Ordinal);
        Assert.Contains("# Environment", dynamicTail, StringComparison.Ordinal);
        Assert.Contains("# Long-Session Maintenance", dynamicTail, StringComparison.Ordinal);
        Assert.Contains("# Memory Hygiene", dynamicTail, StringComparison.Ordinal);
        Assert.Contains("# Tool Playbooks", dynamicTail, StringComparison.Ordinal);
        Assert.Contains("# Runtime Instructions", dynamicTail, StringComparison.Ordinal);
        Assert.Contains("# Request-Specific Instructions", dynamicTail, StringComparison.Ordinal);
        Assert.Equal($"{staticPrefix}{Environment.NewLine}{Environment.NewLine}{dynamicTail}", fullPrompt);
    }

    [Fact]
    public async Task ToolCallScheduler_EmitsStableToolMetadataForLiveToolRendering()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"qwen-tool-events-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var request = CreateTurnRequest(workspaceRoot);
            var scheduler = new ToolCallScheduler(
                new NonInteractiveToolExecutor(
                    new StubToolExecutor(
                        new NativeToolExecutionResult
                        {
                            ToolName = "web_search",
                            Status = "completed",
                            ApprovalState = "allow",
                            WorkingDirectory = workspaceRoot,
                            Output = "forecast data",
                            ChangedFiles = []
                        })),
                new LoopDetectionService());

            var toolHistory = new List<AssistantToolCallResult>();
            var events = new List<AssistantRuntimeEvent>();
            AssistantToolCall[] toolCalls =
            [
                new AssistantToolCall
                {
                    Id = "call-web-search-1",
                    ToolName = "web_search",
                    ArgumentsJson = """{"query":"weather minsk"}"""
                }
            ];

            var result = await scheduler.ScheduleAsync(
                request,
                "qwen-compatible",
                "qwen3-coder-plus",
                toolCalls,
                toolHistory,
                events.Add);

            Assert.True(result.ContinueTurnLoop);
            Assert.Single(toolHistory);

            var requestedEvent = Assert.Single(events, static item => item.Stage == "tool-requested");
            var completedEvent = Assert.Single(events, static item => item.Stage == "tool-completed");
            Assert.Equal("call-web-search-1", requestedEvent.ToolCallId);
            Assert.Equal("call-web-search-1", completedEvent.ToolCallId);
            Assert.False(string.IsNullOrWhiteSpace(requestedEvent.ToolCallGroupId));
            Assert.Equal(requestedEvent.ToolCallGroupId, completedEvent.ToolCallGroupId);
            Assert.Equal("""{"query":"weather minsk"}""", requestedEvent.ToolArgumentsJson);
            Assert.Equal("""{"query":"weather minsk"}""", completedEvent.ToolArgumentsJson);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ToolCallScheduler_ErrorResult_KeepsTurnLoopRunning()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"qwen-tool-error-loop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var request = CreateTurnRequest(workspaceRoot);
            var scheduler = new ToolCallScheduler(
                new NonInteractiveToolExecutor(
                    new StubToolExecutor(
                        new NativeToolExecutionResult
                        {
                            ToolName = "web_fetch",
                            Status = "error",
                            ApprovalState = "allow",
                            WorkingDirectory = workspaceRoot,
                            ErrorMessage = "404 Not Found",
                            ChangedFiles = []
                        })),
                new LoopDetectionService());

            var toolHistory = new List<AssistantToolCallResult>();
            var events = new List<AssistantRuntimeEvent>();
            AssistantToolCall[] toolCalls =
            [
                new AssistantToolCall
                {
                    Id = "call-web-fetch-1",
                    ToolName = "web_fetch",
                    ArgumentsJson = """{"url":"https://example.com/missing","prompt":"Summarize it"}"""
                }
            ];

            var result = await scheduler.ScheduleAsync(
                request,
                "qwen-compatible",
                "qwen3-coder-plus",
                toolCalls,
                toolHistory,
                events.Add);

            Assert.True(result.ContinueTurnLoop);
            var toolResult = Assert.Single(toolHistory);
            Assert.Equal("error", toolResult.Execution.Status);
            Assert.Equal("404 Not Found", toolResult.Execution.ErrorMessage);
            var failedEvent = Assert.Single(events, static item => item.Stage == "tool-failed");
            Assert.Equal("call-web-fetch-1", failedEvent.ToolCallId);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static AssistantTurnRequest CreateTurnRequest(string? workspaceRoot = null)
    {
        var resolvedWorkspace = workspaceRoot ?? Path.Combine(Path.GetTempPath(), $"qwen-prompt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(resolvedWorkspace);
        var runtimeProfile = CreateRuntimeProfile(resolvedWorkspace);

        return new AssistantTurnRequest
        {
            SessionId = "desktop-prompt-session",
            Prompt = "Fix the desktop web tools and explain the result.",
            WorkingDirectory = resolvedWorkspace,
            TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "desktop-prompt-session.jsonl"),
            RuntimeProfile = runtimeProfile,
            GitBranch = "main",
            ToolExecution = new NativeToolExecutionResult
            {
                ToolName = string.Empty,
                Status = "not-requested",
                ApprovalState = "allow",
                WorkingDirectory = resolvedWorkspace,
                Output = string.Empty,
                ErrorMessage = string.Empty,
                ExitCode = 0,
                ChangedFiles = []
            }
        };
    }

    private static AssistantPromptContext CreatePromptContext() =>
        new()
        {
            SessionSummary = "Transcript messages loaded: 2",
            EnvironmentSummary = "Workspace root: D:\\repo",
            SessionGuidanceSummary = "Transcript messages retained for this turn: 2",
            UserInstructionSummary = "From ~/.qwen/QWEN.md:\n- Prefer native runtime integrations.",
            WorkspaceInstructionSummary = "From QWEN.md:\n- Keep reconnect flow stable.",
            DurableMemorySummary = "Project durable memory (QWEN.md):\n- Remember reconnect state.",
            McpServerSummary = "- docs (project): 3 tool(s), 2 prompt(s), resources available",
            ScratchpadSummary = "Use `D:\\runtime\\tmp\\scratchpad\\desktop-prompt-session` for temporary files.",
            LanguageSummary = "Preferred locale: en\nPreferred language: English",
            OutputStyleSummary = "Mode-specific expectation: Prefer concise, action-oriented answers.",
            HistoryHighlights = ["user: fix the web tools", "assistant: investigating runtime behavior"],
            ContextFiles = ["--- Context from: AGENTS.md ---\nUse the tools carefully.\n--- End of Context from: AGENTS.md ---"],
            Messages =
            [
                new AssistantConversationMessage
                {
                    Role = "user",
                    Content = "Previous user turn"
                }
            ]
        };

    private static QwenRuntimeProfile CreateRuntimeProfile(string workspaceRoot)
    {
        var homeRoot = Path.Combine(workspaceRoot, ".test-home");
        Directory.CreateDirectory(homeRoot);

        var chatsDirectory = Path.Combine(homeRoot, "chats");
        var historyDirectory = Path.Combine(homeRoot, "history");
        Directory.CreateDirectory(chatsDirectory);
        Directory.CreateDirectory(historyDirectory);

        return new QwenRuntimeProfile
        {
            ProjectRoot = workspaceRoot,
            GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
            RuntimeBaseDirectory = homeRoot,
            RuntimeSource = "tests",
            ProjectDataDirectory = Path.Combine(homeRoot, "project-data"),
            ChatsDirectory = chatsDirectory,
            HistoryDirectory = historyDirectory,
            ContextFileNames = ["QWEN.md"],
            ContextFilePaths = [],
            CurrentLocale = "en",
            CurrentLanguage = "English",
            ApprovalProfile = new ApprovalProfile
            {
                DefaultMode = "default",
                ConfirmShellCommands = true,
                ConfirmFileEdits = true,
                AllowRules = [],
                AskRules = [],
                DenyRules = []
            }
        };
    }

    private sealed class StubToolExecutor(NativeToolExecutionResult executionResult) : IToolExecutor
    {
        public NativeToolHostSnapshot Inspect(WorkspacePaths paths) => new()
        {
            RegisteredCount = 0,
            ImplementedCount = 0,
            ReadyCount = 0,
            ApprovalRequiredCount = 0,
            Tools = []
        };

        public Task<NativeToolExecutionResult> ExecuteAsync(
            WorkspacePaths paths,
            ExecuteNativeToolRequest request,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(executionResult);
    }
}
