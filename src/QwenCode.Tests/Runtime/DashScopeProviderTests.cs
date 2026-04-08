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
            Assert.True(payload["stream"]?.GetValue<bool>());
            Assert.True(payload["stream_options"]?["include_usage"]?.GetValue<bool>());
            Assert.Null(payload["tool_choice"]);
            Assert.Null(payload["max_tokens"]);
            Assert.Null(payload["temperature"]);
            var toolNames = payload["tools"]!.AsArray()
                .Select(item => item?["function"]?["name"]?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
            Assert.Equal(
                ["agent", "skill", "list_directory", "read_file", "grep_search", "glob", "edit", "write_file", "run_shell_command", "save_memory", "todo_write", "ask_user_question", "exit_plan_mode", "web_fetch", "web_search"],
                toolNames);
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

    [Fact]
    public async Task DashScopeAssistantResponseProvider_TryGenerateAsync_ParsesInterleavedStreamingToolCalls()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-stream-tools-{Guid.NewGuid():N}");
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

            var streamingPayload = """
                data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call-1","function":{"name":"read_file","arguments":"{\"file_"}}]}}]}

                data: {"choices":[{"delta":{"tool_calls":[{"index":1,"id":"call-2","function":{"name":"list_directory","arguments":"{\"path\":\"D:\\\\demo"}}]}}]}

                data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"path\":\"D:\\\\demo\\\\a.txt\"}"}}]}}]}

                data: {"choices":[{"delta":{"tool_calls":[{"index":1,"function":{"arguments":"\"}"}}]}}]}

                data: {"choices":[{"delta":{"content":"ok"},"finish_reason":"tool_calls"}]}

                data: [DONE]
                """;

            var httpClient = new HttpClient(new RecordingHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(streamingPayload, Encoding.UTF8, "text/event-stream")
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
                    Messages = []
                },
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible",
                    ApiKeyEnvironmentVariable = "DASHSCOPE_API_KEY"
                });

            Assert.NotNull(response);
            Assert.Equal("ok", response!.Summary);
            Assert.Equal(2, response.ToolCalls.Count);
            Assert.Equal("read_file", response.ToolCalls[0].ToolName);
            Assert.Equal("""{"file_path":"D:\\demo\\a.txt"}""", response.ToolCalls[0].ArgumentsJson);
            Assert.Equal("list_directory", response.ToolCalls[1].ToolName);
            Assert.Equal("""{"path":"D:\\demo"}""", response.ToolCalls[1].ArgumentsJson);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DashScopeAssistantResponseProvider_TryGenerateAsync_RetriesOnceWithoutStreamingWhenToolCallPayloadIsTruncated()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-stream-retry-{Guid.NewGuid():N}");
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

            var requestBodies = new List<string>();
            var callCount = 0;
            var runtimeEvents = new List<AssistantRuntimeEvent>();
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                callCount++;
                requestBodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
                if (callCount == 1)
                {
                    var truncatedStream = """
                        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call-1","function":{"name":"write_file","arguments":"{\"file_path\":\"D:\\\\demo\\\\a.txt\",\"content\":\"hel"}}]}}]}

                        data: {"choices":[{"finish_reason":"length","delta":{}}]}

                        data: [DONE]
                        """;

                    var firstResponse = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(truncatedStream, Encoding.UTF8, "text/event-stream")
                    };
                    firstResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                    return Task.FromResult(firstResponse);
                }

                var fallbackPayload = """
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "Recovered after non-stream retry",
                            "tool_calls": [
                              {
                                "id": "call-1",
                                "function": {
                                  "name": "write_file",
                                  "arguments": "{\"file_path\":\"D:\\\\demo\\\\a.txt\",\"content\":\"hello\"}"
                                }
                              }
                            ]
                          }
                        }
                      ]
                    }
                    """;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(fallbackPayload, Encoding.UTF8, "application/json")
                });
            }));

            var provider = new DashScopeAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
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
                    Messages = []
                },
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible",
                    ApiKeyEnvironmentVariable = "DASHSCOPE_API_KEY"
                },
                runtimeEvents.Add);

            Assert.NotNull(response);
            Assert.Equal(2, callCount);
            Assert.Equal("Recovered after non-stream retry", response!.Summary);
            var toolCall = Assert.Single(response.ToolCalls);
            Assert.Equal("write_file", toolCall.ToolName);
            Assert.Equal("""{"file_path":"D:\\demo\\a.txt","content":"hello"}""", toolCall.ArgumentsJson);
            Assert.Contains(runtimeEvents, static item => item.Stage == "stream-retry");

            var firstPayload = JsonNode.Parse(requestBodies[0])!.AsObject();
            var secondPayload = JsonNode.Parse(requestBodies[1])!.AsObject();
            Assert.True(firstPayload["stream"]!.GetValue<bool>());
            Assert.False(secondPayload["stream"]!.GetValue<bool>());
            Assert.True(firstPayload["stream_options"]?["include_usage"]?.GetValue<bool>());
            Assert.Null(firstPayload["max_tokens"]);
            Assert.Null(firstPayload["temperature"]);
            Assert.Null(firstPayload["tool_choice"]);
            Assert.Null(secondPayload["max_tokens"]);
            Assert.Null(secondPayload["temperature"]);
            Assert.Null(secondPayload["tool_choice"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DashScopeAssistantResponseProvider_TryGenerateAsync_RetriesAfterEmbeddedStreamingErrorChunk()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-stream-error-{Guid.NewGuid():N}");
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

            var callCount = 0;
            var runtimeEvents = new List<AssistantRuntimeEvent>();
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((_, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var embeddedErrorStream = """
                        data: {"choices":[{"delta":{"content":"rate limit exceeded"},"finish_reason":"error_finish"}]}

                        data: [DONE]
                        """;

                    var streamingResponse = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(embeddedErrorStream, Encoding.UTF8, "text/event-stream")
                    };
                    streamingResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                    return Task.FromResult(streamingResponse);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "choices": [
                            {
                              "message": {
                                "content": "Recovered from embedded stream error"
                              }
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                });
            }));

            var provider = new DashScopeAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
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
                    Messages = []
                },
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible",
                    ApiKeyEnvironmentVariable = "DASHSCOPE_API_KEY"
                },
                runtimeEvents.Add);

            Assert.NotNull(response);
            Assert.Equal(2, callCount);
            Assert.Equal("Recovered from embedded stream error", response!.Summary);
            Assert.Contains(runtimeEvents, static item => item.Stage == "stream-retry" &&
                                                         item.Message.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DashScopeAssistantResponseProvider_TryGenerateAsync_RetriesTransientHttp429Responses()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-http-retry-{Guid.NewGuid():N}");
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
                      "selectedType": "qwen-oauth"
                    }
                  },
                  "model": {
                    "name": "coder-model"
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var store = new QwenCode.App.Auth.FileQwenOAuthCredentialStore(environmentPaths);
            await store.WriteAsync(
                new QwenOAuthCredentials
                {
                    AccessToken = "persisted-oauth-token",
                    RefreshToken = "refresh-token",
                    ResourceUrl = "https://portal.qwen.ai",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
                });

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

            var callCount = 0;
            var runtimeEvents = new List<AssistantRuntimeEvent>();
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((_, _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var retryResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        Content = new StringContent(
                            """{"error":{"code":"rate_limit_exceeded","message":"temporary overload"}}""",
                            Encoding.UTF8,
                            "application/json")
                    };
                    retryResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                    return Task.FromResult(retryResponse);
                }

                var successPayload = """
                    data: {"choices":[{"delta":{"content":"retried "}}]}

                    data: {"choices":[{"delta":{"content":"successfully"}}]}

                    data: [DONE]
                    """;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(successPayload, Encoding.UTF8, "text/event-stream")
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                return Task.FromResult(response);
            }));

            var provider = new DashScopeAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(environmentPaths, store),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "provider-session",
                    Prompt = "Retry a transient rate limit.",
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
                    Provider = "qwen-compatible"
                },
                runtimeEvents.Add);

            Assert.NotNull(response);
            Assert.Equal(2, callCount);
            Assert.Equal("retried successfully", response!.Summary);
            Assert.Contains(runtimeEvents, static item => item.Stage == "provider-retry");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DashScopeAssistantResponseProvider_TryGenerateAsync_DoesNotRetryQwenQuotaExceeded429()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-quota-{Guid.NewGuid():N}");
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
                      "selectedType": "qwen-oauth"
                    }
                  },
                  "model": {
                    "name": "coder-model"
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var store = new QwenCode.App.Auth.FileQwenOAuthCredentialStore(environmentPaths);
            await store.WriteAsync(
                new QwenOAuthCredentials
                {
                    AccessToken = "persisted-oauth-token",
                    RefreshToken = "refresh-token",
                    ResourceUrl = "https://portal.qwen.ai",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
                });

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

            var callCount = 0;
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((_, _) =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(
                        """{"error":{"code":"insufficient_quota","message":"free allocated quota exceeded"}}""",
                        Encoding.UTF8,
                        "application/json")
                });
            }));

            var provider = new DashScopeAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(environmentPaths, store),
                new TokenLimitService());

            var exception = await Assert.ThrowsAsync<AssistantProviderRequestException>(() =>
                provider.TryGenerateAsync(
                    new AssistantTurnRequest
                    {
                        SessionId = "provider-session",
                        Prompt = "Do not retry a hard quota error.",
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
                        Provider = "qwen-compatible"
                    }));

            Assert.Equal(1, callCount);
            Assert.Equal(429, exception.StatusCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
