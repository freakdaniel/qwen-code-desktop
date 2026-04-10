using System.Text.Json;
using QwenCode.Core.Channels;
using QwenCode.Core.Extensions;

namespace QwenCode.Tests.Channels;

public sealed class ChannelRegistryServiceTests
{
    [Fact]
    public void Inspect_ReadsConfiguredChannelsAndRuntimeStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channels-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var qwenRoot = Path.Combine(homeRoot, ".qwen");
            var channelsRoot = Path.Combine(qwenRoot, "channels");
            Directory.CreateDirectory(qwenRoot);
            Directory.CreateDirectory(channelsRoot);

            File.WriteAllText(
                Path.Combine(qwenRoot, "settings.json"),
                """
                {
                  "channels": {
                    "team-telegram": {
                      "type": "telegram",
                      "senderPolicy": "pairing",
                      "sessionScope": "thread",
                      "cwd": ".",
                      "approvalMode": "plan",
                      "model": "qwen-max"
                    }
                  }
                }
                """);

            File.WriteAllText(
                Path.Combine(channelsRoot, "service.pid"),
                JsonSerializer.Serialize(new
                {
                    pid = Environment.ProcessId,
                    startedAt = DateTime.UtcNow.AddMinutes(-5).ToString("O"),
                    channels = new[] { "team-telegram" }
                }));
            File.WriteAllText(
                Path.Combine(channelsRoot, "sessions.json"),
                """
                {
                  "one": { "target": { "channelName": "team-telegram" } },
                  "two": { "target": { "channelName": "team-telegram" } }
                }
                """);
            File.WriteAllText(
                Path.Combine(channelsRoot, "team-telegram-pairing.json"),
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        senderId = "user-1",
                        senderName = "Alice",
                        code = "ABCD2345",
                        createdAt = DateTimeOffset.UtcNow.AddMinutes(-4).ToUnixTimeMilliseconds()
                    }
                }));
            File.WriteAllText(
                Path.Combine(channelsRoot, "team-telegram-allowlist.json"),
                JsonSerializer.Serialize(new[] { "trusted-user" }));

            var snapshot = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.True(snapshot.IsServiceRunning);
            Assert.Contains("telegram", snapshot.SupportedTypes);

            var channel = Assert.Single(snapshot.Channels);
            Assert.Equal("team-telegram", channel.Name);
            Assert.Equal("telegram", channel.Type);
            Assert.Equal("pairing", channel.SenderPolicy);
            Assert.Equal("thread", channel.SessionScope);
            Assert.Equal("running", channel.Status);
            Assert.Equal(2, channel.SessionCount);
            Assert.Equal(1, channel.PendingPairingCount);
            Assert.Equal(1, channel.AllowlistCount);
            Assert.True(channel.SupportsPairing);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetPairings_FiltersExpiredRequests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-pairings-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var qwenRoot = Path.Combine(homeRoot, ".qwen");
            var channelsRoot = Path.Combine(qwenRoot, "channels");
            Directory.CreateDirectory(qwenRoot);
            Directory.CreateDirectory(channelsRoot);

            File.WriteAllText(
                Path.Combine(qwenRoot, "settings.json"),
                """
                {
                  "channels": {
                    "pairing-demo": {
                      "type": "telegram",
                      "senderPolicy": "pairing"
                    }
                  }
                }
                """);

            File.WriteAllText(
                Path.Combine(channelsRoot, "pairing-demo-pairing.json"),
                JsonSerializer.Serialize(new object[]
                {
                    new
                    {
                        senderId = "fresh",
                        senderName = "Fresh User",
                        code = "FRESH123",
                        createdAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds()
                    },
                    new
                    {
                        senderId = "expired",
                        senderName = "Expired User",
                        code = "OLD12345",
                        createdAt = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeMilliseconds()
                    }
                }));

            var snapshot = service.GetPairings(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetChannelPairingRequest { Name = "pairing-demo" });

            Assert.Equal(1, snapshot.PendingCount);
            var request = Assert.Single(snapshot.PendingRequests);
            Assert.Equal("fresh", request.SenderId);
            Assert.Equal("FRESH123", request.Code);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ApprovePairing_MovesSenderToAllowlist()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-approve-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var qwenRoot = Path.Combine(homeRoot, ".qwen");
            var channelsRoot = Path.Combine(qwenRoot, "channels");
            Directory.CreateDirectory(qwenRoot);
            Directory.CreateDirectory(channelsRoot);

            File.WriteAllText(
                Path.Combine(qwenRoot, "settings.json"),
                """
                {
                  "channels": {
                    "pairing-demo": {
                      "type": "telegram",
                      "senderPolicy": "pairing"
                    }
                  }
                }
                """);

            File.WriteAllText(
                Path.Combine(channelsRoot, "pairing-demo-pairing.json"),
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        senderId = "user-42",
                        senderName = "Tester",
                        code = "PAIR4242",
                        createdAt = DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds()
                    }
                }));

            var snapshot = service.ApprovePairing(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ApproveChannelPairingRequest
                {
                    Name = "pairing-demo",
                    Code = "pair4242"
                });

            Assert.Equal(0, snapshot.PendingCount);
            Assert.Equal(1, snapshot.AllowlistCount);
            var allowlist = JsonSerializer.Deserialize<string[]>(
                File.ReadAllText(Path.Combine(channelsRoot, "pairing-demo-allowlist.json")));
            Assert.NotNull(allowlist);
            Assert.Contains("user-42", allowlist);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ChannelRegistryService CreateService(string homeRoot)
    {
        var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null, homeRoot, homeRoot);
        var runtimeProfile = new QwenRuntimeProfileService(environmentPaths);
        var compatibility = new QwenCompatibilityService(environmentPaths);
        return new ChannelRegistryService(
            environmentPaths,
            new DesktopSettingsResolver(compatibility, runtimeProfile),
            new ExtensionCatalogService(runtimeProfile, environmentPaths));
    }
}
