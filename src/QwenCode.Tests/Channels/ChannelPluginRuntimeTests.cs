using System.Text.Json;
using QwenCode.App.Channels;
using QwenCode.App.Extensions;
using QwenCode.App.Models;
using QwenCode.App.Sessions;

namespace QwenCode.Tests.Channels;

public sealed class ChannelPluginRuntimeTests
{
    [Fact]
    public async Task ChannelRuntimeService_HandleInboundAsync_LoadsExtensionPluginChannel()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-plugin-{Guid.NewGuid():N}");
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
            await File.WriteAllTextAsync(
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
            await File.WriteAllTextAsync(
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

                        const response = await bridge.prompt(sessionId, `[plugin] ${envelope.text}`, {});
                        await this.sendMessage(envelope.chatId, response);
                      },
                      async sendMessage(_chatId, _text) {}
                    };
                  }
                };
                """);

            await File.WriteAllTextAsync(
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

            var sessionHost = new RecordingPluginSessionHost();
            var runtime = CreateRuntime(workspaceRoot, homeRoot, systemRoot, sessionHost);
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
                Assert.Equal("plugin-response", result.AssistantSummary);
                Assert.Contains("[plugin] What is 2+2?", sessionHost.LastPrompt, StringComparison.Ordinal);
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

    private static ChannelRuntimeService CreateRuntime(
        string workspaceRoot,
        string homeRoot,
        string systemRoot,
        ISessionHost sessionHost)
    {
        var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
        var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
        var compatibilityService = new QwenCompatibilityService(environmentPaths);
        var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
        var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
        var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
        var router = new ChannelSessionRouterService(environmentPaths);
        var adapters = new IChannelAdapter[]
        {
            new TelegramChannelAdapter(new HttpClient()),
            new WeixinChannelAdapter(new HttpClient(), environmentPaths),
            new DingtalkChannelAdapter(new HttpClient())
        };
        var deliveryService = new ChannelDeliveryService(
            sessionHost,
            registry,
            router,
            adapters,
            environmentPaths);
        var pluginRuntime = new ChannelPluginRuntimeService(
            new ChannelPluginRegistryService(extensionCatalog),
            registry,
            sessionHost);

        return new ChannelRuntimeService(
            registry,
            pluginRuntime,
            adapters,
            router,
            environmentPaths,
            deliveryService,
            sessionHost);
    }

    private sealed class RecordingPluginSessionHost : ISessionHost
    {
        public event EventHandler<DesktopSessionEvent>? SessionEvent;

        public string LastPrompt { get; private set; } = string.Empty;

        public Task<DesktopSessionTurnResult> StartTurnAsync(WorkspacePaths paths, StartDesktopSessionTurnRequest request, CancellationToken cancellationToken = default)
        {
            LastPrompt = request.Prompt;
            SessionEvent?.Invoke(this, new DesktopSessionEvent
            {
                SessionId = request.SessionId,
                Kind = DesktopSessionEventKind.AssistantStreaming,
                TimestampUtc = DateTime.UtcNow,
                Message = "chunk",
                ContentDelta = "plugin-",
                WorkingDirectory = request.WorkingDirectory
            });
            SessionEvent?.Invoke(this, new DesktopSessionEvent
            {
                SessionId = request.SessionId,
                Kind = DesktopSessionEventKind.AssistantStreaming,
                TimestampUtc = DateTime.UtcNow,
                Message = "chunk",
                ContentDelta = "response",
                WorkingDirectory = request.WorkingDirectory
            });

            return Task.FromResult(new DesktopSessionTurnResult
            {
                Session = new SessionPreview
                {
                    SessionId = request.SessionId,
                    Title = "plugin channel",
                    LastActivity = DateTimeOffset.UtcNow.ToString("O"),
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
                AssistantSummary = "plugin-response",
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
}
