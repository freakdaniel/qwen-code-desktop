using System.Text.Json;
using System.Net;
using System.Text;
using QwenCode.Core.Channels;
using QwenCode.Core.Sessions;

namespace QwenCode.Tests.Channels;

public sealed class ChannelDeliveryServiceTests
{
    [Fact]
    public async Task ChannelDeliveryService_DeliverAsync_WritesOutboxEntryForAssistantMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-outbox-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var homeRoot = Path.Combine(root, "home");
            var workspaceRoot = Path.Combine(root, "workspace");
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "channels": {
                    "ops": {
                      "type": "telegram",
                      "senderPolicy": "open",
                      "sessionScope": "user",
                      "cwd": "."
                    }
                  }
                }
                """);

            var sessionHost = new FakeSessionHost();
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null);
            var router = new ChannelSessionRouterService(environmentPaths);
            _ = await router.ResolveAsync("ops", "user", "user-1", "chat-1", string.Empty, string.Empty, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);

            var service = new ChannelDeliveryService(
                sessionHost,
                registry,
                router,
                [new TelegramChannelAdapter(new HttpClient())],
                environmentPaths);

            await service.DeliverAsync(new DesktopSessionEvent
            {
                SessionId = router.ListRoutes().Single().SessionId,
                Kind = DesktopSessionEventKind.AssistantCompleted,
                TimestampUtc = DateTime.UtcNow,
                Message = "hello from assistant"
            });

            var outboxPath = Path.Combine(homeRoot, ".qwen", "channels", "outbox", "ops.jsonl");
            Assert.True(File.Exists(outboxPath));
            var line = File.ReadLines(outboxPath).Single();
            using var document = JsonDocument.Parse(line);
            Assert.Equal("message", document.RootElement.GetProperty("kind").GetString());
            Assert.Equal("hello from assistant", document.RootElement.GetProperty("text").GetString());
            Assert.Equal("chat-1", document.RootElement.GetProperty("payload").GetProperty("chatId").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChannelDeliveryService_DeliverAsync_SendsTelegramMessage_WhenTokenConfigured()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-send-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var homeRoot = Path.Combine(root, "home");
            var workspaceRoot = Path.Combine(root, "workspace");
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
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

            var sessionHost = new FakeSessionHost();
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null);
            var router = new ChannelSessionRouterService(environmentPaths);
            _ = await router.ResolveAsync("ops", "user", "user-1", "chat-1", string.Empty, string.Empty, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
            var handler = new RecordingHttpMessageHandler();

            var service = new ChannelDeliveryService(
                sessionHost,
                registry,
                router,
                [new TelegramChannelAdapter(new HttpClient(handler))],
                environmentPaths);

            await service.DeliverAsync(new DesktopSessionEvent
            {
                SessionId = router.ListRoutes().Single().SessionId,
                Kind = DesktopSessionEventKind.AssistantCompleted,
                TimestampUtc = DateTime.UtcNow,
                Message = "hello live delivery"
            });

            Assert.Single(handler.Requests);
            Assert.Contains("telegram-token", handler.Requests[0]);

            var outboxPath = Path.Combine(homeRoot, ".qwen", "channels", "outbox", "ops.jsonl");
            using var document = JsonDocument.Parse(File.ReadLines(outboxPath).Single());
            Assert.Equal("delivered", document.RootElement.GetProperty("deliveryStatus").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChannelDeliveryService_DeliverAsync_BlockStreaming_FlushesBlocksWithoutDuplicatingFinalMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-block-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var homeRoot = Path.Combine(root, "home");
            var workspaceRoot = Path.Combine(root, "workspace");
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
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
                      "token": "telegram-token",
                      "blockStreaming": "on",
                      "blockStreamingChunk": {
                        "minChars": 5,
                        "maxChars": 10
                      },
                      "blockStreamingCoalesce": {
                        "idleMs": 0
                      }
                    }
                  }
                }
                """);

            var sessionHost = new FakeSessionHost();
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null);
            var router = new ChannelSessionRouterService(environmentPaths);
            var route = await router.ResolveAsync("ops", "user", "user-1", "chat-1", string.Empty, string.Empty, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
            var handler = new RecordingHttpMessageHandler();

            var service = new ChannelDeliveryService(
                sessionHost,
                registry,
                router,
                [new TelegramChannelAdapter(new HttpClient(handler))],
                environmentPaths);

            await service.DeliverAsync(new DesktopSessionEvent
            {
                SessionId = route.SessionId,
                Kind = DesktopSessionEventKind.AssistantStreaming,
                TimestampUtc = DateTime.UtcNow,
                Message = string.Empty,
                ContentDelta = "Hello world again"
            });

            await service.DeliverAsync(new DesktopSessionEvent
            {
                SessionId = route.SessionId,
                Kind = DesktopSessionEventKind.AssistantCompleted,
                TimestampUtc = DateTime.UtcNow,
                Message = "Hello world again"
            });

            Assert.Equal(3, handler.Requests.Count);

            var outboxPath = Path.Combine(homeRoot, ".qwen", "channels", "outbox", "ops.jsonl");
            var entries = File.ReadLines(outboxPath).ToArray();
            Assert.Equal(3, entries.Length);
            Assert.All(entries, line =>
            {
                using var document = JsonDocument.Parse(line);
                Assert.Equal("message-block", document.RootElement.GetProperty("kind").GetString());
                Assert.Equal("delivered", document.RootElement.GetProperty("deliveryStatus").GetString());
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeSessionHost : ISessionHost
    {
        public event EventHandler<DesktopSessionEvent>? SessionEvent;

        public Task<DesktopSessionTurnResult> StartTurnAsync(WorkspacePaths paths, StartDesktopSessionTurnRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(WorkspacePaths paths, ApproveDesktopSessionToolRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(WorkspacePaths paths, AnswerDesktopSessionQuestionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CancelDesktopSessionTurnResult> CancelTurnAsync(WorkspacePaths paths, CancelDesktopSessionTurnRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(WorkspacePaths paths, ResumeInterruptedTurnRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(WorkspacePaths paths, DismissInterruptedTurnRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add($"{request.RequestUri}|{body}");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }
}
