using System.Text.Json;
using QwenCode.App.Channels;

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

    private static ChannelRuntimeService CreateRuntime(string workspaceRoot, string homeRoot, string systemRoot)
    {
        var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
        var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
        var compatibilityService = new QwenCompatibilityService(environmentPaths);
        var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
        var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
        var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);
        var sessionHost = TestServiceFactory.CreateSessionHost(runtimeProfileService, compatibilityService);

        return new ChannelRuntimeService(
            registry,
            [new TelegramChannelAdapter(), new WeixinChannelAdapter(), new DingtalkChannelAdapter()],
            new ChannelSessionRouterService(environmentPaths),
            environmentPaths,
            sessionHost);
    }
}
