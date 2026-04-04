using System.Text.Json;
using QwenCode.App.Channels;
using QwenCode.App.Models;

namespace QwenCode.Tests.Channels;

public sealed class ChannelRuntimeServiceTests
{
    [Fact]
    public async Task HandleInboundAsync_PairingChannel_ReturnsPairingCodeAndPersistsPendingRequest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-pairing-{Guid.NewGuid():N}");
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
                      "senderPolicy": "pairing",
                      "sessionScope": "user",
                      "cwd": "."
                    }
                  }
                }
                """);

            var runtime = CreateRuntime(workspaceRoot, homeRoot, systemRoot);
            var result = await runtime.HandleInboundAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "ops",
                JsonDocument.Parse("""{"senderId":"user-1","senderName":"Alice","chatId":"chat-1","text":"hello"}""").RootElement);

            var pairingPath = Path.Combine(homeRoot, ".qwen", "channels", "ops-pairing.json");
            Assert.Equal("pairing-required", result.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.PairingCode));
            Assert.True(File.Exists(pairingPath));
            Assert.Contains(result.PairingCode, File.ReadAllText(pairingPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task HandleInboundAsync_OpenChannel_ReusesSessionRouteForRepeatedMessages()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-routing-{Guid.NewGuid():N}");
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
                      "cwd": "."
                    }
                  }
                }
                """);

            var runtime = CreateRuntime(workspaceRoot, homeRoot, systemRoot);
            var payload = JsonDocument.Parse("""{"senderId":"user-1","senderName":"Alice","chatId":"chat-1","text":"hello"}""").RootElement;

            var first = await runtime.HandleInboundAsync(new WorkspacePaths { WorkspaceRoot = workspaceRoot }, "ops", payload);
            var second = await runtime.HandleInboundAsync(new WorkspacePaths { WorkspaceRoot = workspaceRoot }, "ops", payload);

            Assert.Equal("dispatched", first.Status);
            Assert.Equal(first.SessionId, second.SessionId);
            Assert.True(first.CreatedNewSession);
            Assert.False(second.CreatedNewSession);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_AndStopAsync_UpdateServiceStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-service-{Guid.NewGuid():N}");
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
                      "cwd": "."
                    }
                  }
                }
                """);

            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var registry = new ChannelRegistryService(
                environmentPaths,
                new DesktopSettingsResolver(
                    new QwenCompatibilityService(environmentPaths),
                    new QwenRuntimeProfileService(environmentPaths)),
                new ExtensionCatalogService(
                    new QwenRuntimeProfileService(environmentPaths),
                    environmentPaths));
            var runtime = CreateRuntime(workspaceRoot, homeRoot, systemRoot);

            await runtime.StartAsync(workspace);
            var startedSnapshot = registry.Inspect(workspace);
            Assert.True(startedSnapshot.IsServiceRunning);

            await runtime.StopAsync();
            var stoppedSnapshot = registry.Inspect(workspace);
            Assert.False(stoppedSnapshot.IsServiceRunning);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_ReplaysQueuedOutboxEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-replay-{Guid.NewGuid():N}");
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

            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
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
                text = "queued reply",
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
                    text = "queued reply",
                    toolName = string.Empty,
                    commandName = string.Empty
                }
            };
            File.WriteAllText(
                Path.Combine(outboxRoot, "ops.jsonl"),
                JsonSerializer.Serialize(replayRecord) + Environment.NewLine);

            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
            var handler = new RecordingHttpMessageHandler();
            var deliveryService = new ChannelDeliveryService(
                new FakeSessionHost(),
                registry,
                router,
                [new TelegramChannelAdapter(new HttpClient(handler))],
                environmentPaths);
            var runtime = new ChannelRuntimeService(
                registry,
                [new TelegramChannelAdapter(new HttpClient(handler))],
                router,
                environmentPaths,
                deliveryService,
                new FakeSessionHost());

            _ = await runtime.StartAsync(workspace);

            Assert.Single(handler.Requests);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(outboxRoot, "ops.jsonl")));
            Assert.Equal("delivered", document.RootElement.GetProperty("deliveryStatus").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_BackgroundDrainLoop_ReplaysQueuedEntriesAddedAfterStartup()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-background-replay-{Guid.NewGuid():N}");
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

            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var router = new ChannelSessionRouterService(environmentPaths);
            var route = await router.ResolveAsync("ops", "user", "user-1", "chat-1", string.Empty, string.Empty, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
            var handler = new RecordingHttpMessageHandler();
            var adapters = new IChannelAdapter[] { new TelegramChannelAdapter(new HttpClient(handler)) };
            var deliveryService = new ChannelDeliveryService(new FakeSessionHost(), registry, router, adapters, environmentPaths);
            var runtime = new ChannelRuntimeService(registry, adapters, router, environmentPaths, deliveryService, new FakeSessionHost());

            _ = await runtime.StartAsync(workspace);

            var outboxRoot = Path.Combine(homeRoot, ".qwen", "channels", "outbox");
            Directory.CreateDirectory(outboxRoot);
            var replayRecord = new
            {
                channelName = "ops",
                sessionId = route.SessionId,
                chatId = "chat-1",
                senderId = "user-1",
                kind = "message",
                text = "background queued reply",
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
                    text = "background queued reply",
                    toolName = string.Empty,
                    commandName = string.Empty
                }
            };
            var outboxPath = Path.Combine(outboxRoot, "ops.jsonl");
            File.WriteAllText(outboxPath, JsonSerializer.Serialize(replayRecord) + Environment.NewLine);

            var delivered = await WaitForAsync(async () =>
            {
                if (handler.Requests.Count == 0 || !File.Exists(outboxPath))
                {
                    return false;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(outboxPath));
                return string.Equals(document.RootElement.GetProperty("deliveryStatus").GetString(), "delivered", StringComparison.OrdinalIgnoreCase);
            }, TimeSpan.FromSeconds(3));

            Assert.True(delivered);
            await runtime.StopAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_AndStopAsync_InvokeAdapterLifecycle()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-lifecycle-{Guid.NewGuid():N}");
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
            var adapter = new TrackingTelegramAdapter();
            var router = new ChannelSessionRouterService(environmentPaths);
            var deliveryService = new ChannelDeliveryService(new FakeSessionHost(), registry, router, [adapter], environmentPaths);
            var runtime = new ChannelRuntimeService(
                registry,
                [adapter],
                router,
                environmentPaths,
                deliveryService,
                new FakeSessionHost());

            _ = await runtime.StartAsync(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            await runtime.StopAsync();

            Assert.Equal(1, adapter.ConnectCalls);
            Assert.Equal(1, adapter.DisconnectCalls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ChannelRuntimeService CreateRuntime(string workspaceRoot, string homeRoot, string systemRoot)
    {
        var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
        var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
        var compatibilityService = new QwenCompatibilityService(environmentPaths);
        var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
        var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
        var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
        var sessionHost = TestServiceFactory.CreateSessionHost(runtimeProfileService, compatibilityService);
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

        return new ChannelRuntimeService(
            registry,
            adapters,
            router,
            environmentPaths,
            deliveryService,
            sessionHost);
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
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class TrackingTelegramAdapter : ChannelAdapterBase
    {
        public TrackingTelegramAdapter() : base("telegram")
        {
        }

        public int ConnectCalls { get; private set; }

        public int DisconnectCalls { get; private set; }

        public override Task ConnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            return Task.CompletedTask;
        }

        public override Task DisconnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default)
        {
            DisconnectCalls++;
            return Task.CompletedTask;
        }

        public override ChannelEnvelope NormalizeInbound(string channelName, JsonElement payload) =>
            throw new NotSupportedException();
    }

    private static async Task<bool> WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }
}
