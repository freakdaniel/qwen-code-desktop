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
            Assert.Contains("loop detection", response.Summary, StringComparison.OrdinalIgnoreCase);
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
