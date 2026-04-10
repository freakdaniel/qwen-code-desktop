using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.Core.Channels;
using QwenCode.Core.Extensions;
using QwenCode.Core.Ide;
using QwenCode.Core.Sessions;
using QwenCode.Tests.Shared.Fixtures;

namespace QwenCode.Tests.Parity;

public sealed class RuntimeParityHarnessTests
{
    [Fact]
    public async Task ProviderStreamingHarness_OpenAiCompatible_RecoversStructuredToolCalls()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-parity-provider-{Guid.NewGuid():N}");
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
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((_, _) =>
            {
                var streamPayload = """
                    data: {"choices":[{"delta":{"content":"hello "}}]}

                    data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call-1","function":{"name":"read_file","arguments":"{\"file_"}}]}}]}

                    data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"path\":\"D:\\\\repo\\\\README.md\"}"}}]}}]}

                    data: {"choices":[{"delta":{"content":"world"},"finish_reason":"tool_calls"}]}

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
                new AssistantTurnRequest
                {
                    SessionId = "parity-provider-session",
                    Prompt = "Continue",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "parity-provider-session.jsonl"),
                    RuntimeProfile = runtimeProfile,
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
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
                    Provider = "openai-compatible",
                    ApiKeyEnvironmentVariable = "OPENAI_API_KEY"
                });

            Assert.NotNull(response);
            Assert.Equal("hello world", response!.Summary);
            var toolCall = Assert.Single(response.ToolCalls);
            Assert.Equal("read_file", toolCall.ToolName);
            Assert.Equal("""{"file_path":"D:\\repo\\README.md"}""", toolCall.ArgumentsJson);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderStreamingHarness_OpenRouter_AddsProviderHeaders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-parity-openrouter-{Guid.NewGuid():N}");
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

            var runtimeProfile = CreateRuntimeProfile(workspaceRoot, homeRoot);
            var provider = new OpenAiCompatibleAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService());

            var response = await provider.TryGenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "parity-openrouter-session",
                    Prompt = "Continue",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "parity-openrouter-session.jsonl"),
                    RuntimeProfile = runtimeProfile,
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
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
    public async Task ChannelsHarness_Runtime_RespectsCollectDefaultsAndAttachmentPromptShaping()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-parity-channel-{Guid.NewGuid():N}");
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
                  "channels": {
                    "ops": {
                      "type": "telegram",
                      "senderPolicy": "open",
                      "sessionScope": "user",
                      "instructions": "Follow channel-safe tone",
                      "cwd": "."
                    }
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
            var host = new RecordingParitySessionHost();
            var router = new ChannelSessionRouterService(environmentPaths);
            var adapters = new IChannelAdapter[]
            {
                new TelegramChannelAdapter(new HttpClient()),
                new WeixinChannelAdapter(new HttpClient(), environmentPaths),
                new DingtalkChannelAdapter(new HttpClient())
            };
            var deliveryService = new ChannelDeliveryService(
                host,
                registry,
                router,
                adapters,
                environmentPaths);
            var pluginRuntime = new ChannelPluginRuntimeService(
                new ChannelPluginRegistryService(extensionCatalog),
                registry,
                host);
            var runtime = new ChannelRuntimeService(
                registry,
                pluginRuntime,
                adapters,
                router,
                environmentPaths,
                deliveryService,
                host);

            var configuration = registry.GetRuntimeConfiguration(new WorkspacePaths { WorkspaceRoot = workspaceRoot }, "ops");
            var result = await runtime.HandleInboundAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "ops",
                JsonDocument.Parse(
                    """
                    {
                      "senderId":"user-1",
                      "senderName":"Alice",
                      "chatId":"chat-1",
                      "text":"review the attachment",
                      "imageBase64":"abcd",
                      "imageMimeType":"image/png",
                      "attachments":[
                        {"type":"file","fileName":"notes.txt","filePath":"D:\\notes.txt","mimeType":"text/plain"}
                      ]
                    }
                    """).RootElement);

            Assert.Equal("collect", configuration.DispatchMode);
            Assert.Equal("dispatched", result.Status);
            Assert.Contains("Follow channel-safe tone", host.LastPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Embedded image attached", host.LastPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("notes.txt", host.LastPrompt, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChannelsHarness_Runtime_ReplaysQueuedOutboxEntriesOnStart()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-parity-channel-replay-{Guid.NewGuid():N}");
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
                  "channels": {
                    "ops": {
                      "type": "telegram",
                      "senderPolicy": "open",
                      "sessionScope": "user",
                      "cwd": ".",
                      "token": "telegram-token"
                    }
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
            var router = new ChannelSessionRouterService(environmentPaths);
            var route = await router.ResolveAsync("ops", "user", "user-1", "chat-1", string.Empty, string.Empty, workspaceRoot);
            var outboxRoot = Path.Combine(homeRoot, ".qwen", "channels", "outbox");
            Directory.CreateDirectory(outboxRoot);
            var replayRecord = new
            {
                channelName = "ops",
                sessionId = route.SessionId,
                chatId = "chat-1",
                senderId = "user-1",
                kind = "message",
                text = "replay me",
                toolName = string.Empty,
                commandName = string.Empty,
                timestampUtc = DateTime.UtcNow,
                deliveryStatus = "queued",
                payload = new
                {
                    channel = "ops",
                    chatId = "chat-1",
                    senderId = "user-1",
                    sessionId = route.SessionId,
                    threadId = string.Empty,
                    replyAddress = string.Empty,
                    workingDirectory = workspaceRoot,
                    kind = "message",
                    text = "replay me",
                    toolName = string.Empty,
                    commandName = string.Empty
                }
            };
            File.WriteAllText(
                Path.Combine(outboxRoot, "ops.jsonl"),
                JsonSerializer.Serialize(replayRecord) + Environment.NewLine);

            var handler = new RecordingHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                }));
            var adapters = new IChannelAdapter[]
            {
                new TelegramChannelAdapter(new HttpClient(handler)),
                new WeixinChannelAdapter(new HttpClient(handler), environmentPaths),
                new DingtalkChannelAdapter(new HttpClient(handler))
            };
            var host = new RecordingParitySessionHost();
            var deliveryService = new ChannelDeliveryService(host, registry, router, adapters, environmentPaths);
            var pluginRuntime = new ChannelPluginRuntimeService(
                new ChannelPluginRegistryService(extensionCatalog),
                registry,
                host);
            var runtime = new ChannelRuntimeService(registry, pluginRuntime, adapters, router, environmentPaths, deliveryService, host);

            _ = await runtime.StartAsync(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.Single(handler.RequestBodies);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(outboxRoot, "ops.jsonl")));
            Assert.Equal("delivered", document.RootElement.GetProperty("deliveryStatus").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChannelsHarness_PluginRuntime_LoadsExtensionPluginAndBridgesPromptFlow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-parity-channel-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var extensionRoot = Path.Combine(homeRoot, ".qwen", "extensions", "plugin-example");
            Directory.CreateDirectory(extensionRoot);
            File.WriteAllText(
                Path.Combine(extensionRoot, "qwen-extension.json"),
                """
                {
                  "name": "plugin-example",
                  "version": "1.0.0",
                  "channels": {
                    "plugin-example": {
                      "entry": "entry.mjs",
                      "displayName": "Plugin Example"
                    }
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(extensionRoot, "entry.mjs"),
                """
                export const plugin = {
                  channelType: 'plugin-example',
                  displayName: 'Plugin Example',
                  requiredConfigFields: ['serverWsUrl'],
                  createChannel(name, config, bridge) {
                    const sessions = new Map();
                    return {
                      async connect() {},
                      async disconnect() {},
                      async handleInbound(envelope) {
                        const key = `${envelope.senderId}:${envelope.chatId}`;
                        let sessionId = sessions.get(key);
                        if (!sessionId) {
                          sessionId = await bridge.newSession(config.workingDirectory || process.cwd());
                          sessions.set(key, sessionId);
                        }

                        const response = await bridge.prompt(sessionId, `[plugin-harness] ${envelope.text}`, {});
                        await this.sendMessage(envelope.chatId, response);
                      },
                      async sendMessage(_chatId, _text) {}
                    };
                  }
                };
                """);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "channels": {
                    "plugin-ops": {
                      "type": "plugin-example",
                      "serverWsUrl": "ws://localhost:9201",
                      "senderPolicy": "open",
                      "sessionScope": "user",
                      "cwd": "."
                    }
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
            var host = new RecordingParitySessionHost();
            var router = new ChannelSessionRouterService(environmentPaths);
            var adapters = new IChannelAdapter[]
            {
                new TelegramChannelAdapter(new HttpClient()),
                new WeixinChannelAdapter(new HttpClient(), environmentPaths),
                new DingtalkChannelAdapter(new HttpClient())
            };
            var deliveryService = new ChannelDeliveryService(host, registry, router, adapters, environmentPaths);
            var pluginRuntime = new ChannelPluginRuntimeService(
                new ChannelPluginRegistryService(extensionCatalog),
                registry,
                host);
            var runtime = new ChannelRuntimeService(
                registry,
                pluginRuntime,
                adapters,
                router,
                environmentPaths,
                deliveryService,
                host);

            try
            {
                var result = await runtime.HandleInboundAsync(
                    new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                    "plugin-ops",
                    JsonDocument.Parse(
                        """
                        {
                          "senderId":"user-1",
                          "senderName":"Alice",
                          "chatId":"chat-1",
                          "text":"What is 2+2?"
                        }
                        """).RootElement);

                Assert.Equal("dispatched", result.Status);
                Assert.Equal("ok", result.AssistantSummary);
                Assert.Contains("[plugin-harness] What is 2+2?", host.LastPrompt, StringComparison.Ordinal);
            }
            finally
            {
                await runtime.StopAsync();
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IdeHarness_Backend_UsesEnvironmentFallbackAndNormalizesContext()
    {
        lock (ProcessEnvironmentLock.Gate)
        {
            var root = Path.Combine(Path.GetTempPath(), $"qwen-parity-ide-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            var previousPort = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_SERVER_PORT");
            var previousWorkspace = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_WORKSPACE_PATH");
            var previousAuth = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_AUTH_TOKEN");

            try
            {
                var homeRoot = Path.Combine(root, "home");
                var workspaceRoot = Path.Combine(root, "workspace");
                Directory.CreateDirectory(homeRoot);
                Directory.CreateDirectory(workspaceRoot);

                Environment.SetEnvironmentVariable("QWEN_CODE_IDE_SERVER_PORT", "4222");
                Environment.SetEnvironmentVariable("QWEN_CODE_IDE_WORKSPACE_PATH", workspaceRoot);
                Environment.SetEnvironmentVariable("QWEN_CODE_IDE_AUTH_TOKEN", "secret");

                var contextService = new IdeContextService();
                var backend = new IdeBackendService(
                    new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot),
                    new IdeDetectionService(),
                    contextService,
                    new IdeInstallerService(new NoOpIdeCommandRunner(), new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot)),
                    new AlwaysAliveProcessProbe());

                var normalizedContext = backend.UpdateContext(new IdeContextSnapshot
                {
                    OpenFiles =
                    [
                        new IdeOpenFile
                        {
                            Path = "b.cs",
                            Timestamp = 2,
                            IsActive = true,
                            SelectedText = new string('x', IdeContextService.MaxSelectedTextLength + 10)
                        },
                        new IdeOpenFile
                        {
                            Path = "a.cs",
                            Timestamp = 1,
                            IsActive = true,
                            SelectedText = "stale"
                        }
                    ],
                    IsTrusted = true
                });
                var snapshot = backend.Inspect(workspaceRoot, "code");

                Assert.Equal("connected", snapshot.Status);
                Assert.Equal("4222", snapshot.Port);
                Assert.Equal("***", snapshot.AuthToken);
                Assert.True(normalizedContext.OpenFiles[0].IsActive);
                Assert.Contains("[TRUNCATED]", normalizedContext.OpenFiles[0].SelectedText);
                Assert.False(normalizedContext.OpenFiles[1].IsActive);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QWEN_CODE_IDE_SERVER_PORT", previousPort);
                Environment.SetEnvironmentVariable("QWEN_CODE_IDE_WORKSPACE_PATH", previousWorkspace);
                Environment.SetEnvironmentVariable("QWEN_CODE_IDE_AUTH_TOKEN", previousAuth);
                Directory.Delete(root, recursive: true);
            }
        }
    }

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

    private sealed class RecordingParitySessionHost : ISessionHost
    {
        public event EventHandler<DesktopSessionEvent>? SessionEvent;

        public string LastPrompt { get; private set; } = string.Empty;

        public Task<DesktopSessionTurnResult> StartTurnAsync(WorkspacePaths paths, StartDesktopSessionTurnRequest request, CancellationToken cancellationToken = default)
        {
            LastPrompt = request.Prompt;
            return Task.FromResult(new DesktopSessionTurnResult
            {
                Session = new SessionPreview
                {
                    SessionId = "parity-channel-session",
                    Title = "channel",
                    LastActivity = "now",
                    StartedAt = DateTimeOffset.UtcNow.ToString("O"),
                    LastUpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
                    Category = "channel",
                    Mode = DesktopMode.Code,
                    Status = "ready",
                    WorkingDirectory = request.WorkingDirectory,
                    GitBranch = "main",
                    TranscriptPath = Path.Combine(request.WorkingDirectory, ".qwen", "session.jsonl"),
                    MetadataPath = Path.Combine(request.WorkingDirectory, ".qwen", "session.meta.json")
                },
                AssistantSummary = "ok",
                CreatedNewSession = true,
                ToolExecution = new NativeToolExecutionResult
                {
                    ToolName = string.Empty,
                    Status = "not-requested",
                    ApprovalState = "allow",
                    WorkingDirectory = request.WorkingDirectory,
                    ChangedFiles = []
                }
            });
        }

        public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(WorkspacePaths paths, ApproveDesktopSessionToolRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(WorkspacePaths paths, AnswerDesktopSessionQuestionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CancelDesktopSessionTurnResult> CancelTurnAsync(WorkspacePaths paths, CancelDesktopSessionTurnRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CancelDesktopSessionTurnResult
            {
                SessionId = request.SessionId,
                Cancelled = true,
                Message = "cancelled",
                TimestampUtc = DateTime.UtcNow
            });

        public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(WorkspacePaths paths, ResumeInterruptedTurnRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(WorkspacePaths paths, DismissInterruptedTurnRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class AlwaysAliveProcessProbe : IIdeProcessProbe
    {
        public bool Exists(int processId) => true;
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
            return await responder(request, cancellationToken);
        }
    }

    private sealed class NoOpIdeCommandRunner : IIdeCommandRunner
    {
        public Task<IdeCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, bool useShellExecute = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IdeCommandResult
            {
                ExitCode = 0,
                StandardOutput = string.Empty,
                StandardError = string.Empty
            });
    }
}
