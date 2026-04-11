namespace QwenCode.Tests.Runtime;

public sealed class AssistantTurnRuntimeTests
{
    [Fact]
    public async Task AssistantTurnRuntime_ExecutesToolCallsAndReturnsFinalSummary()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-tool-loop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var targetFile = Path.Combine(workspaceRoot, "sample.txt");
            File.WriteAllText(targetFile, "native tool content");

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var approvalPolicyService = new ApprovalPolicyService();
            var runtime = CreateAssistantTurnRuntime(
                new ToolCallingAssistantResponseProvider(targetFile),
                new NativeToolHostService(runtimeProfileService, approvalPolicyService));

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var response = await runtime.GenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "tool-loop-session",
                    Prompt = "Inspect the target file and summarize the result.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "tool-loop-session.jsonl"),
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

            Assert.Equal("tool-provider", response.ProviderName);
            Assert.Equal("Tool loop complete after 1 native execution(s).", response.Summary);
            Assert.Equal("completed", response.StopReason);
            Assert.Equal(2, response.Stats.RoundCount);
            Assert.Equal(1, response.Stats.ToolCallCount);
            Assert.Equal(1, response.Stats.SuccessfulToolCallCount);
            Assert.Equal(0, response.Stats.FailedToolCallCount);
            Assert.True(response.Stats.DurationMs >= 0);
            var toolExecution = Assert.Single(response.ToolExecutions);
            Assert.Equal("read_file", toolExecution.Execution.ToolName);
            Assert.Equal("completed", toolExecution.Execution.Status);
            Assert.Contains("native tool content", toolExecution.Execution.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantTurnRuntime_StopsOnRepeatedToolLoop()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-loop-stop-{Guid.NewGuid():N}");
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
            var runtime = new AssistantTurnRuntime(
                new AssistantPromptAssembler(new ProjectSummaryService()),
                [new RepeatingToolCallingAssistantResponseProvider(), new FallbackAssistantResponseProvider()],
                new ToolCallScheduler(
                    new NonInteractiveToolExecutor(
                        new SequencedToolExecutor(
                            new NativeToolExecutionResult
                            {
                                ToolName = "read_file",
                                Status = "completed",
                                ApprovalState = "allow",
                                WorkingDirectory = workspaceRoot,
                                Output = "loop-1",
                                ChangedFiles = []
                            },
                            new NativeToolExecutionResult
                            {
                                ToolName = "read_file",
                                Status = "completed",
                                ApprovalState = "allow",
                                WorkingDirectory = workspaceRoot,
                                Output = "loop-2",
                                ChangedFiles = []
                            },
                            new NativeToolExecutionResult
                            {
                                ToolName = "read_file",
                                Status = "completed",
                                ApprovalState = "allow",
                                WorkingDirectory = workspaceRoot,
                                Output = "loop-3",
                                ChangedFiles = []
                            },
                            new NativeToolExecutionResult
                            {
                                ToolName = "read_file",
                                Status = "completed",
                                ApprovalState = "allow",
                                WorkingDirectory = workspaceRoot,
                                Output = "loop-4",
                                ChangedFiles = []
                            })),
                    new LoopDetectionService()),
                new LoopDetectionService(),
                new TokenLimitService(),
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                Options.Create(new NativeAssistantRuntimeOptions
                {
                    Provider = "loop-provider",
                    MaxToolIterations = 6
                }));

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var response = await runtime.GenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "tool-loop-detected",
                    Prompt = "Keep trying the same tool forever.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "tool-loop-detected.jsonl"),
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

            Assert.Equal("loop-provider", response.ProviderName);
            Assert.Contains("same tool call", response.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("tool-loop-detected", response.StopReason);
            Assert.Equal(4, response.Stats.ToolCallCount);
            Assert.Equal(0, response.Stats.SuccessfulToolCallCount);
            Assert.Equal(4, response.Stats.FailedToolCallCount);
            Assert.Equal(4, response.ToolExecutions.Count);
            Assert.All(response.ToolExecutions, item => Assert.Equal("read_file", item.Execution.ToolName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantTurnRuntime_StopsAfterApprovalRequiredToolWithoutCallingProviderAgain()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-approval-stop-{Guid.NewGuid():N}");
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
            var provider = new ApprovalGateAssistantResponseProvider();
            var runtime = TestServiceFactory.CreateAssistantTurnRuntime(
                provider,
                new SequencedToolExecutor(
                    new NativeToolExecutionResult
                    {
                        ToolName = "write_file",
                        Status = "approval-required",
                        ApprovalState = "ask",
                        WorkingDirectory = workspaceRoot,
                        ErrorMessage = "Approval is required for write_file.",
                        ChangedFiles = []
                    }));

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var response = await runtime.GenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "approval-stop-session",
                    Prompt = "Write a new implementation file.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "approval-stop-session.jsonl"),
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

            Assert.Equal(1, provider.InvocationCount);
            Assert.Contains("waiting for approval", response.Summary, StringComparison.OrdinalIgnoreCase);
            var toolExecution = Assert.Single(response.ToolExecutions);
            Assert.Equal("write_file", toolExecution.Execution.ToolName);
            Assert.Equal("approval-required", toolExecution.Execution.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantTurnRuntime_SeedsApprovedToolResultWithoutReturningDuplicateExecution()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-approval-seed-{Guid.NewGuid():N}");
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
            var provider = new ApprovalResolutionAssistantResponseProvider();
            var runtime = TestServiceFactory.CreateAssistantTurnRuntime(provider);

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var response = await runtime.GenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "approval-resolution-session",
                    Prompt = "The user approved tool 'run_shell_command'. Continue from the updated tool result.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "approval-resolution-session.jsonl"),
                    RuntimeProfile = runtimeProfile,
                    GitBranch = "main",
                    ToolArgumentsJson = """{"command":"curl -s https://example.com"}""",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = "run_shell_command",
                        Status = "completed",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = "approved command output",
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    },
                    IsApprovalResolution = true
                });

            Assert.Equal(1, provider.InvocationCount);
            var seededToolResult = Assert.Single(provider.SeenToolHistory);
            Assert.Equal("run_shell_command", seededToolResult.ToolCall.ToolName);
            Assert.Equal("""{"command":"curl -s https://example.com"}""", seededToolResult.ToolCall.ArgumentsJson);
            Assert.Equal("approved command output", seededToolResult.Execution.Output);
            Assert.Empty(response.ToolExecutions);
            Assert.Equal(0, response.Stats.ToolCallCount);
            Assert.Equal("completed", response.StopReason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantTurnRuntime_SchedulesMultipleToolCallsBeforeContinuingGeneration()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-multi-tool-{Guid.NewGuid():N}");
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
            var provider = new MultiToolAssistantResponseProvider();
            var runtime = TestServiceFactory.CreateAssistantTurnRuntime(
                provider,
                new SequencedToolExecutor(
                    new NativeToolExecutionResult
                    {
                        ToolName = "read_file",
                        Status = "completed",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = "first tool output",
                        ChangedFiles = []
                    },
                    new NativeToolExecutionResult
                    {
                        ToolName = "list_directory",
                        Status = "completed",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = "second tool output",
                        ChangedFiles = []
                    }));

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var response = await runtime.GenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "multi-tool-session",
                    Prompt = "Inspect the file and list the directory.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "multi-tool-session.jsonl"),
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

            Assert.Equal(2, provider.InvocationCount);
            Assert.Equal("Two-tool loop complete after 2 native execution(s).", response.Summary);
            Assert.Equal(2, response.ToolExecutions.Count);
            Assert.Equal("read_file", response.ToolExecutions[0].Execution.ToolName);
            Assert.Equal("list_directory", response.ToolExecutions[1].Execution.ToolName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantTurnRuntime_ContinuesAfterFailedToolAndLetsProviderRecover()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-tool-error-recovery-{Guid.NewGuid():N}");
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
            var provider = new ToolErrorRecoveryAssistantResponseProvider();
            var runtime = TestServiceFactory.CreateAssistantTurnRuntime(
                provider,
                new SequencedToolExecutor(
                    new NativeToolExecutionResult
                    {
                        ToolName = "web_fetch",
                        Status = "error",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        ErrorMessage = "404 Not Found",
                        ChangedFiles = []
                    }));

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var response = await runtime.GenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "tool-error-recovery-session",
                    Prompt = "Find the latest Avalonia dev notes and summarize them.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "tool-error-recovery-session.jsonl"),
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

            Assert.Equal(2, provider.InvocationCount);
            Assert.Equal("tool-error-recovery-provider", response.ProviderName);
            Assert.Equal("completed", response.StopReason);
            Assert.Equal("Recovered after analyzing the failed tool result and switching strategy.", response.Summary);
            var toolExecution = Assert.Single(response.ToolExecutions);
            Assert.Equal("web_fetch", toolExecution.Execution.ToolName);
            Assert.Equal("error", toolExecution.Execution.Status);
            Assert.Equal("404 Not Found", toolExecution.Execution.ErrorMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssistantTurnRuntime_ReturnsProviderErrorInsteadOfFallbackPlaceholder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-provider-error-{Guid.NewGuid():N}");
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
            var runtime = new AssistantTurnRuntime(
                new AssistantPromptAssembler(new ProjectSummaryService()),
                [new ThrowingAssistantResponseProvider(), new FallbackAssistantResponseProvider()],
                new ToolCallScheduler(
                    new NonInteractiveToolExecutor(new SequencedToolExecutor()),
                    new LoopDetectionService()),
                new LoopDetectionService(),
                new TokenLimitService(),
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                Options.Create(new NativeAssistantRuntimeOptions
                {
                    Provider = "failing-provider",
                    Model = "coder-model"
                }));

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var response = await runtime.GenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "provider-error-session",
                    Prompt = "Say hello.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "provider-error-session.jsonl"),
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

            Assert.Equal("failing-provider", response.ProviderName);
            Assert.Equal("provider-error", response.StopReason);
            Assert.Contains("HTTP 401", response.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Turn recorded in the native desktop session host", response.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ApprovalGateAssistantResponseProvider : IAssistantResponseProvider
    {
        public int InvocationCount { get; private set; }

        public string Name => "approval-provider";

        public Task<AssistantTurnResponse?> TryGenerateAsync(
            AssistantTurnRequest request,
            AssistantPromptContext promptContext,
            IReadOnlyList<AssistantToolCallResult> toolHistory,
            NativeAssistantRuntimeOptions options,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;

            return Task.FromResult<AssistantTurnResponse?>(
                InvocationCount == 1
                    ? new AssistantTurnResponse
                    {
                        Summary = string.Empty,
                        ProviderName = Name,
                        Model = "approval-provider-model",
                        ToolCalls =
                        [
                            new AssistantToolCall
                            {
                                Id = "approval-call-1",
                                ToolName = "write_file",
                                ArgumentsJson = """{"file_path":"D:\\temp\\blocked.txt","content":"blocked"}"""
                            }
                        ]
                    }
                    : new AssistantTurnResponse
                    {
                        Summary = "Provider should not have been called again after approval-required.",
                        ProviderName = Name,
                        Model = "approval-provider-model"
                    });
        }
    }

    private sealed class MultiToolAssistantResponseProvider : IAssistantResponseProvider
    {
        public int InvocationCount { get; private set; }

        public string Name => "multi-tool-provider";

        public Task<AssistantTurnResponse?> TryGenerateAsync(
            AssistantTurnRequest request,
            AssistantPromptContext promptContext,
            IReadOnlyList<AssistantToolCallResult> toolHistory,
            NativeAssistantRuntimeOptions options,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;

            if (InvocationCount == 1)
            {
                return Task.FromResult<AssistantTurnResponse?>(
                    new AssistantTurnResponse
                    {
                        Summary = string.Empty,
                        ProviderName = Name,
                        Model = "multi-tool-provider-model",
                        ToolCalls =
                        [
                            new AssistantToolCall
                            {
                                Id = "tool-call-1",
                                ToolName = "read_file",
                                ArgumentsJson = """{"file_path":"D:\\temp\\one.txt"}"""
                            },
                            new AssistantToolCall
                            {
                                Id = "tool-call-2",
                                ToolName = "list_directory",
                                ArgumentsJson = """{"path":"D:\\temp"}"""
                            }
                        ]
                    });
            }

            return Task.FromResult<AssistantTurnResponse?>(
                new AssistantTurnResponse
                {
                    Summary = $"Two-tool loop complete after {toolHistory.Count} native execution(s).",
                    ProviderName = Name,
                    Model = "multi-tool-provider-model"
                });
        }
    }

    private sealed class ApprovalResolutionAssistantResponseProvider : IAssistantResponseProvider
    {
        public int InvocationCount { get; private set; }

        public IReadOnlyList<AssistantToolCallResult> SeenToolHistory { get; private set; } = [];

        public string Name => "approval-resolution-provider";

        public Task<AssistantTurnResponse?> TryGenerateAsync(
            AssistantTurnRequest request,
            AssistantPromptContext promptContext,
            IReadOnlyList<AssistantToolCallResult> toolHistory,
            NativeAssistantRuntimeOptions options,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            SeenToolHistory = toolHistory.ToArray();

            return Task.FromResult<AssistantTurnResponse?>(
                new AssistantTurnResponse
                {
                    Summary = $"Continued with {toolHistory.Count} approved tool result(s).",
                    ProviderName = Name,
                    Model = "approval-resolution-model"
                });
        }
    }

    private sealed class ThrowingAssistantResponseProvider : IAssistantResponseProvider
    {
        public string Name => "failing-provider";

        public Task<AssistantTurnResponse?> TryGenerateAsync(
            AssistantTurnRequest request,
            AssistantPromptContext promptContext,
            IReadOnlyList<AssistantToolCallResult> toolHistory,
            NativeAssistantRuntimeOptions options,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default) =>
            throw new AssistantProviderRequestException(
                Name,
                "https://portal.qwen.ai/v1/chat/completions",
                401,
                "{\"error\":\"invalid_token\"}");
    }

    private sealed class ToolErrorRecoveryAssistantResponseProvider : IAssistantResponseProvider
    {
        public int InvocationCount { get; private set; }

        public string Name => "tool-error-recovery-provider";

        public Task<AssistantTurnResponse?> TryGenerateAsync(
            AssistantTurnRequest request,
            AssistantPromptContext promptContext,
            IReadOnlyList<AssistantToolCallResult> toolHistory,
            NativeAssistantRuntimeOptions options,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;

            if (InvocationCount == 1)
            {
                return Task.FromResult<AssistantTurnResponse?>(
                    new AssistantTurnResponse
                    {
                        Summary = string.Empty,
                        ProviderName = Name,
                        Model = "tool-error-recovery-model",
                        ToolCalls =
                        [
                            new AssistantToolCall
                            {
                                Id = "tool-error-recovery-call-1",
                                ToolName = "web_fetch",
                                ArgumentsJson = """{"url":"https://avaloniaui.net/blog/whats-new-in-avalonia-12","prompt":"Summarize the release notes"}"""
                            }
                        ]
                    });
            }

            var failedResult = Assert.Single(toolHistory);
            Assert.Equal("error", failedResult.Execution.Status);
            Assert.Equal("web_fetch", failedResult.Execution.ToolName);

            return Task.FromResult<AssistantTurnResponse?>(
                new AssistantTurnResponse
                {
                    Summary = "Recovered after analyzing the failed tool result and switching strategy.",
                    ProviderName = Name,
                    Model = "tool-error-recovery-model"
                });
        }
    }

    private sealed class SequencedToolExecutor(params NativeToolExecutionResult[] results) : IToolExecutor
    {
        private readonly Queue<NativeToolExecutionResult> queuedResults = new(results);

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
            CancellationToken cancellationToken = default)
        {
            if (queuedResults.Count == 0)
            {
                throw new InvalidOperationException("No more queued tool execution results are available.");
            }

            var next = queuedResults.Dequeue();
            return Task.FromResult(next);
        }
    }


}
