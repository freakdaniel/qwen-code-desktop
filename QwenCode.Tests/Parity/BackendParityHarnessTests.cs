using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using QwenCode.App.Auth;
using QwenCode.App.Ide;
using QwenCode.App.Telemetry;

namespace QwenCode.Tests.Parity;

public sealed class BackendParityHarnessTests
{
    [Fact]
    public void UpstreamIntegrationFixtures_AreAvailableForParityHarness()
    {
        var root = @"D:\Projects\qwen-code-main\integration-tests";

        Assert.True(Directory.Exists(root));
        Assert.True(File.Exists(Path.Combine(root, "cli", "settings-migration.test.ts")));
        Assert.True(File.Exists(Path.Combine(root, "hook-integration", "hooks.test.ts")));
        Assert.True(File.Exists(Path.Combine(root, "sdk-typescript", "single-turn.test.ts")));
        Assert.True(File.Exists(Path.Combine(root, "cli", "telemetry.test.ts")));
        Assert.True(File.Exists(Path.Combine(root, "channel-plugin.test.ts")));
    }

    [Fact]
    public void IdeWorkspaceValidation_MatchesExpectedParityShape()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"qwen-parity-workspace-{Guid.NewGuid():N}");
        var nestedRoot = Path.Combine(workspaceRoot, "nested");
        Directory.CreateDirectory(nestedRoot);

        try
        {
            var valid = IdeBackendService.ValidateWorkspacePath(workspaceRoot, nestedRoot);
            var invalid = IdeBackendService.ValidateWorkspacePath(Path.Combine(workspaceRoot, "other"), nestedRoot);

            Assert.True(valid.IsValid);
            Assert.False(invalid.IsValid);
            Assert.Contains("Directory mismatch", invalid.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TelemetryHarness_WritesJsonlEventsAndMetricAggregates()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-parity-telemetry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);

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
                Telemetry = new RuntimeTelemetrySettings
                {
                    Enabled = true,
                    Outfile = "telemetry/events.jsonl",
                    LogPrompts = false
                },
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

            var telemetry = new TelemetryService(NullLogger<TelemetryService>.Instance);
            await telemetry.TrackUserPromptAsync(runtimeProfile, "session-1", "prompt-1", "super secret prompt", "openrouter");
            var snapshot = await telemetry.GetSnapshotAsync(runtimeProfile);
            var lines = File.ReadAllLines(snapshot.EventsPath);

            Assert.Single(lines);
            using var document = JsonDocument.Parse(lines[0]);
            Assert.Equal("user_prompt", document.RootElement.GetProperty("event.name").GetString());
            Assert.Equal("session-1", document.RootElement.GetProperty("session_id").GetString());
            Assert.Equal("[redacted]", document.RootElement.GetProperty("prompt").GetString());
            Assert.True(snapshot.Metrics.ContainsKey("qwen.prompt.count"));
            Assert.Equal(1, snapshot.EventCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("openrouter", "OPENROUTER_API_KEY", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("deepseek", "DEEPSEEK_API_KEY", "https://api.deepseek.com/v1/chat/completions")]
    [InlineData("modelscope", "MODELSCOPE_API_KEY", "https://api.modelscope.cn/v1/chat/completions")]
    public void AuthAliasHarness_ReportsExpectedEndpointAndCredentialKey(
        string authType,
        string envKey,
        string expectedEndpoint)
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-parity-auth-{authType}-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                $$"""
                {
                  "security": {
                    "auth": {
                      "selectedType": "{{authType}}"
                    }
                  },
                  "model": {
                    "name": "provider-model"
                  }
                }
                """);

            Environment.SetEnvironmentVariable(envKey, "provider-key");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var authService = new AuthFlowService(
                runtimeProfileService,
                environmentPaths,
                new FileQwenOAuthCredentialStore(environmentPaths),
                new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)))),
                new FakeAuthUrlLauncher());

            var snapshot = authService.GetStatus(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.Equal(authType, snapshot.SelectedType);
            Assert.Equal(envKey, snapshot.ApiKeyEnvironmentVariable);
            Assert.Equal(expectedEndpoint, snapshot.Endpoint);
            Assert.True(snapshot.HasApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
            Directory.Delete(root, recursive: true);
        }
    }
}
