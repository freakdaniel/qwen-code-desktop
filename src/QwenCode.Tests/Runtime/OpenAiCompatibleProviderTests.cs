using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace QwenCode.Tests.Runtime;

public sealed class OpenAiCompatibleProviderTests
{
    [Fact]
    public async Task OpenAiCompatibleAssistantResponseProvider_TryGenerateAsync_ParsesStreamingToolCalls()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-openai-stream-{Guid.NewGuid():N}");
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
                  "security": {
                    "auth": {
                      "selectedType": "openai",
                      "baseUrl": "https://example.test/v1"
                    }
                  },
                  "env": {
                    "OPENAI_API_KEY": "openai-key"
                  },
                  "model": {
                    "name": "gpt-4.1"
                  }
                }
                """);

            var runtimeProfile = CreateRuntimeProfile(workspaceRoot, homeRoot);
            var payloads = new List<string>();
            var events = new List<AssistantRuntimeEvent>();
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                payloads.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
                var streamPayload = """
                    data: {"choices":[{"delta":{"content":"openai "}}]}

                    data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call-1","function":{"name":"read_file","arguments":"{\"file_"}}]}}]}

                    data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"path\":\"D:\\\\repo\\\\sample.txt\"}"}}]}}]}

                    data: {"choices":[{"delta":{"content":"done"},"finish_reason":"tool_calls"}]}

                    data: [DONE]
                    """;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(streamPayload, Encoding.UTF8, "text/event-stream")
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                return Task.FromResult(response);
            }));

            var provider = new OpenAiCompatibleAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
                CreateTurnRequest(workspaceRoot, runtimeProfile),
                CreatePromptContext(),
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "openai-compatible",
                    ApiKeyEnvironmentVariable = "OPENAI_API_KEY"
                },
                events.Add);

            Assert.NotNull(response);
            Assert.Equal("openai-compatible", response!.ProviderName);
            Assert.Equal("gpt-4.1", response.Model);
            Assert.Equal("openai done", response.Summary);
            var toolCall = Assert.Single(response.ToolCalls);
            Assert.Equal("read_file", toolCall.ToolName);
            Assert.Equal("""{"file_path":"D:\\repo\\sample.txt"}""", toolCall.ArgumentsJson);
            Assert.Contains(events, static item => item.Stage == "response-delta");

            var requestPayload = JsonNode.Parse(payloads.Single())!.AsObject();
            Assert.True(requestPayload["stream"]!.GetValue<bool>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenAiCompatibleAssistantResponseProvider_TryGenerateAsync_RetriesOnceWithoutStreamingWhenStreamIsTruncated()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-openai-stream-retry-{Guid.NewGuid():N}");
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
                  "security": {
                    "auth": {
                      "selectedType": "openai",
                      "baseUrl": "https://example.test/v1"
                    }
                  },
                  "env": {
                    "OPENAI_API_KEY": "openai-key"
                  },
                  "model": {
                    "name": "gpt-4.1"
                  }
                }
                """);

            var runtimeProfile = CreateRuntimeProfile(workspaceRoot, homeRoot);
            var payloads = new List<string>();
            var callCount = 0;
            var events = new List<AssistantRuntimeEvent>();
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                callCount++;
                payloads.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
                if (callCount == 1)
                {
                    var streamPayload = """
                        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call-1","function":{"name":"write_file","arguments":"{\"file_path\":\"D:\\\\repo\\\\a.txt\",\"content\":\"hel"}}]}}]}

                        data: {"choices":[{"finish_reason":"length","delta":{}}]}

                        data: [DONE]
                        """;
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(streamPayload, Encoding.UTF8, "text/event-stream")
                    };
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                    return Task.FromResult(response);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "choices": [
                            {
                              "message": {
                                "content": "Recovered after retry",
                                "tool_calls": [
                                  {
                                    "id": "call-1",
                                    "function": {
                                      "name": "write_file",
                                      "arguments": "{\"file_path\":\"D:\\\\repo\\\\a.txt\",\"content\":\"hello\"}"
                                    }
                                  }
                                ]
                              }
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                });
            }));

            var provider = new OpenAiCompatibleAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
                CreateTurnRequest(workspaceRoot, runtimeProfile),
                CreatePromptContext(),
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "openai-compatible",
                    ApiKeyEnvironmentVariable = "OPENAI_API_KEY"
                },
                events.Add);

            Assert.NotNull(response);
            Assert.Equal(2, callCount);
            Assert.Equal("Recovered after retry", response!.Summary);
            Assert.Contains(events, static item => item.Stage == "stream-retry");

            var firstPayload = JsonNode.Parse(payloads[0])!.AsObject();
            var secondPayload = JsonNode.Parse(payloads[1])!.AsObject();
            Assert.True(firstPayload["stream"]!.GetValue<bool>());
            Assert.False(secondPayload["stream"]!.GetValue<bool>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenAiCompatibleAssistantResponseProvider_TryGenerateAsync_AddsOpenRouterHeaders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-openrouter-headers-{Guid.NewGuid():N}");
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
                  "security": {
                    "auth": {
                      "selectedType": "openrouter"
                    }
                  },
                  "env": {
                    "OPENROUTER_API_KEY": "openrouter-key"
                  },
                  "model": {
                    "name": "openai/gpt-4.1"
                  }
                }
                """);

            var runtimeProfile = CreateRuntimeProfile(workspaceRoot, homeRoot);
            HttpRequestMessage? capturedRequest = null;
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                capturedRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "choices": [
                            {
                              "message": {
                                "content": "ok"
                              }
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                });
            }));

            var provider = new OpenAiCompatibleAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
                CreateTurnRequest(workspaceRoot, runtimeProfile),
                CreatePromptContext(),
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "openai-compatible"
                });

            Assert.NotNull(response);
            Assert.NotNull(capturedRequest);
            Assert.Equal("https://github.com/QwenLM/qwen-code.git", capturedRequest!.Headers.GetValues("HTTP-Referer").Single());
            Assert.Equal("Qwen Code", capturedRequest.Headers.GetValues("X-OpenRouter-Title").Single());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenAiCompatibleAssistantResponseProvider_TryGenerateAsync_RemovesStreamOptionsForModelScopeFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-modelscope-fallback-{Guid.NewGuid():N}");
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
                  "security": {
                    "auth": {
                      "selectedType": "modelscope"
                    }
                  },
                  "env": {
                    "MODELSCOPE_API_KEY": "modelscope-key"
                  },
                  "model": {
                    "name": "qwen-max"
                  }
                }
                """);

            var runtimeProfile = CreateRuntimeProfile(workspaceRoot, homeRoot);
            var payloads = new List<string>();
            var callCount = 0;
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                callCount++;
                payloads.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
                if (callCount == 1)
                {
                    var streamPayload = """
                        data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call-1","function":{"name":"write_file","arguments":"{\"file_path\":\"D:\\\\repo\\\\a.txt\",\"content\":\"hel"}}]}}]}

                        data: {"choices":[{"finish_reason":"length","delta":{}}]}

                        data: [DONE]
                        """;
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(streamPayload, Encoding.UTF8, "text/event-stream")
                    };
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                    return Task.FromResult(response);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "choices": [
                            {
                              "message": {
                                "content": "Recovered"
                              }
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                });
            }));

            var provider = new OpenAiCompatibleAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
                CreateTurnRequest(workspaceRoot, runtimeProfile),
                CreatePromptContext(),
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "openai-compatible"
                });

            Assert.NotNull(response);
            var firstPayload = JsonNode.Parse(payloads[0])!.AsObject();
            var secondPayload = JsonNode.Parse(payloads[1])!.AsObject();
            Assert.True(firstPayload["stream"]!.GetValue<bool>());
            Assert.NotNull(firstPayload["stream_options"]);
            Assert.False(secondPayload["stream"]!.GetValue<bool>());
            Assert.Null(secondPayload["stream_options"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenAiCompatibleAssistantResponseProvider_TryGenerateAsync_RetriesTransientHttp429Responses()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-openai-http-retry-{Guid.NewGuid():N}");
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
                  "security": {
                    "auth": {
                      "selectedType": "openai",
                      "baseUrl": "https://example.test/v1"
                    }
                  },
                  "env": {
                    "OPENAI_API_KEY": "openai-key"
                  },
                  "model": {
                    "name": "gpt-4.1"
                  }
                }
                """);

            var runtimeProfile = CreateRuntimeProfile(workspaceRoot, homeRoot);
            var callCount = 0;
            var events = new List<AssistantRuntimeEvent>();
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

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "choices": [
                            {
                              "message": {
                                "content": "Recovered after HTTP retry"
                              }
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                });
            }));

            var provider = new OpenAiCompatibleAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
                CreateTurnRequest(workspaceRoot, runtimeProfile),
                CreatePromptContext(),
                [],
                new NativeAssistantRuntimeOptions
                {
                    Provider = "openai-compatible",
                    ApiKeyEnvironmentVariable = "OPENAI_API_KEY"
                },
                events.Add);

            Assert.NotNull(response);
            Assert.Equal(2, callCount);
            Assert.Equal("Recovered after HTTP retry", response!.Summary);
            Assert.Contains(events, static item => item.Stage == "provider-retry");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static AssistantTurnRequest CreateTurnRequest(string workspaceRoot, QwenRuntimeProfile runtimeProfile) =>
        new()
        {
            SessionId = "openai-provider-session",
            Prompt = "Continue the coding session",
            WorkingDirectory = workspaceRoot,
            TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "openai-provider-session.jsonl"),
            RuntimeProfile = runtimeProfile,
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
        };

    private static AssistantPromptContext CreatePromptContext() =>
        new()
        {
            SessionSummary = "Transcript messages loaded: 0",
            HistoryHighlights = [],
            ContextFiles = [],
            Messages = []
        };

    private static QwenRuntimeProfile CreateRuntimeProfile(string workspaceRoot, string homeRoot) =>
        new()
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
}
