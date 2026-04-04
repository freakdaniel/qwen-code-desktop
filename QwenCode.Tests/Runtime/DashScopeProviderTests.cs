namespace QwenCode.Tests.Runtime;

public sealed class DashScopeProviderTests
{
    [Fact]
    public async Task DashScopeAssistantResponseProvider_TryGenerateAsync_UsesResolvedSettingsAndDashScopeHeaders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-http-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "openai"
                    }
                  },
                  "env": {
                    "DASHSCOPE_API_KEY": "dashscope-settings-key"
                  },
                  "model": {
                    "name": "qwen3-coder-plus",
                    "generationConfig": {
                      "customHeaders": {
                        "X-Custom-Settings": "enabled"
                      },
                      "extra_body": {
                        "reasoning_mode": "high"
                      }
                    }
                  }
                }
                """);

            var runtimeProfile = new QwenRuntimeProfile
            {
                ProjectRoot = workspaceRoot,
                GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeBaseDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeSource = "project-settings",
                ProjectDataDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test"),
                ChatsDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test", "chats"),
                HistoryDirectory = Path.Combine(homeRoot, ".qwen", "history", "test"),
                ContextFileNames = ["QWEN.md"],
                ContextFilePaths = [Path.Combine(workspaceRoot, "QWEN.md")],
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

            HttpRequestMessage? capturedRequest = null;
            string? capturedPayload = null;
            var runtimeEvents = new List<AssistantRuntimeEvent>();
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                capturedRequest = request;
                capturedPayload = request.Content is null
                    ? null
                    : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var responsePayload = """
                    data: {"choices":[{"delta":{"content":"qwen "}}]}

                    data: {"choices":[{"delta":{"content":"runtime "}}]}

                    data: {"choices":[{"delta":{"content":"summary"}}]}

                    data: [DONE]
                    """;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responsePayload, Encoding.UTF8, "text/event-stream")
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                return Task.FromResult(response);
            }));

            var provider = new DashScopeAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());
            var response = await provider.TryGenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "provider-session",
                    Prompt = "Generate the next coding step.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "provider-session.jsonl"),
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
                },
                new AssistantPromptContext
                {
                    SessionSummary = "Transcript messages loaded: 0",
                    HistoryHighlights = [],
                    ContextFiles = [],
                    Messages = []
                },
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible",
                    Model = string.Empty,
                    Endpoint = string.Empty,
                    ApiKey = string.Empty,
                    ApiKeyEnvironmentVariable = "DASHSCOPE_API_KEY"
                },
                runtimeEvents.Add);

            Assert.NotNull(response);
            Assert.Equal("qwen-compatible", response!.ProviderName);
            Assert.Equal("qwen3-coder-plus", response.Model);
            Assert.Equal("qwen runtime summary", response.Summary);
            Assert.Equal(3, runtimeEvents.Count);
            Assert.All(runtimeEvents, item => Assert.Equal("response-delta", item.Stage));
            Assert.Equal("qwen runtime summary", runtimeEvents[^1].ContentSnapshot);

            Assert.NotNull(capturedRequest);
            Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", capturedRequest!.RequestUri!.ToString());
            Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
            Assert.Equal("dashscope-settings-key", capturedRequest.Headers.Authorization?.Parameter);
            Assert.Equal("enable", capturedRequest.Headers.GetValues("X-DashScope-CacheControl").Single());
            Assert.Equal("openai", capturedRequest.Headers.GetValues("X-DashScope-AuthType").Single());
            Assert.Equal("enabled", capturedRequest.Headers.GetValues("X-Custom-Settings").Single());

            var payload = JsonNode.Parse(capturedPayload!)!.AsObject();
            Assert.Equal("provider-session", payload["metadata"]?["sessionId"]?.GetValue<string>());
            Assert.Equal("desktop", payload["metadata"]?["channel"]?.GetValue<string>());
            Assert.Equal("high", payload["reasoning_mode"]?.GetValue<string>());
            Assert.Equal("auto", payload["tool_choice"]?.GetValue<string>());
            Assert.True(payload["stream"]?.GetValue<bool>());
            Assert.Equal(32768, payload["max_tokens"]?.GetValue<int>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DashScopeAssistantResponseProvider_TryGenerateAsync_EmitsStructuredToolContinuationMessages()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-tool-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "env": {
                    "DASHSCOPE_API_KEY": "dashscope-settings-key"
                  },
                  "model": {
                    "name": "qwen3-coder-plus"
                  }
                }
                """);

            var runtimeProfile = new QwenRuntimeProfile
            {
                ProjectRoot = workspaceRoot,
                GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeBaseDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeSource = "project-settings",
                ProjectDataDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test"),
                ChatsDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test", "chats"),
                HistoryDirectory = Path.Combine(homeRoot, ".qwen", "history", "test"),
                ContextFileNames = ["QWEN.md"],
                ContextFilePaths = [],
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

            string? capturedPayload = null;
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                capturedPayload = request.Content is null
                    ? null
                    : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var responsePayload = """
                    data: {"choices":[{"delta":{"content":"done"}}]}

                    data: [DONE]
                    """;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responsePayload, Encoding.UTF8, "text/event-stream")
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                return Task.FromResult(response);
            }));

            var provider = new DashScopeAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            await provider.TryGenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "provider-session",
                    Prompt = "Continue after tool execution.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "provider-session.jsonl"),
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
                },
                new AssistantPromptContext
                {
                    SessionSummary = "Transcript messages loaded: 0",
                    HistoryHighlights = [],
                    ContextFiles = [],
                    Messages = [],
                    InputTokenLimit = 131072,
                    ApproximateInputCharacterBudget = 1000
                },
                [
                    new AssistantToolCallResult
                    {
                        ToolCall = new AssistantToolCall
                        {
                            Id = "call-1",
                            ToolName = "read_file",
                            ArgumentsJson = """{"file_path":"D:\\demo\\sample.txt"}"""
                        },
                        Execution = new NativeToolExecutionResult
                        {
                            ToolName = "read_file",
                            Status = "completed",
                            ApprovalState = "allow",
                            WorkingDirectory = workspaceRoot,
                            Output = "file contents",
                            ErrorMessage = string.Empty,
                            ExitCode = 0,
                            ChangedFiles = []
                        }
                    }
                ],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible",
                    ApiKeyEnvironmentVariable = "DASHSCOPE_API_KEY"
                });

            var payload = JsonNode.Parse(capturedPayload!)!.AsObject();
            var messages = payload["messages"]!.AsArray();
            var assistantToolCallMessage = messages[2]!.AsObject();
            Assert.Equal("assistant", assistantToolCallMessage["role"]?.GetValue<string>());
            Assert.Equal("call-1", assistantToolCallMessage["tool_calls"]?[0]?["id"]?.GetValue<string>());
            Assert.Equal("read_file", assistantToolCallMessage["tool_calls"]?[0]?["function"]?["name"]?.GetValue<string>());

            var toolMessage = messages[3]!.AsObject();
            Assert.Equal("tool", toolMessage["role"]?.GetValue<string>());
            Assert.Equal("call-1", toolMessage["tool_call_id"]?.GetValue<string>());
            Assert.Contains("Status: completed", toolMessage["content"]?.GetValue<string>());
            Assert.Contains("file contents", toolMessage["content"]?.GetValue<string>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
