using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using QwenCode.Core.Telemetry;

namespace QwenCode.Tests.Telemetry;

public sealed class TelemetryServiceTests
{
    [Fact]
    public async Task TelemetryService_TrackUserPromptAsync_RedactsPromptContentWhenPromptLoggingDisabled()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-telemetry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var runtimeProfile = CreateRuntimeProfile(root, logPrompts: false);
            var telemetry = new TelemetryService(NullLogger<TelemetryService>.Instance);

            await telemetry.TrackUserPromptAsync(runtimeProfile, "session-1", "prompt-1", "very secret prompt", "qwen-oauth");

            var snapshot = await telemetry.GetSnapshotAsync(runtimeProfile);
            var content = await File.ReadAllTextAsync(snapshot.EventsPath);

            Assert.Equal(1, snapshot.EventCount);
            Assert.Contains("\"event.name\":\"user_prompt\"", content);
            Assert.DoesNotContain("very secret prompt", content);
            Assert.Contains("[redacted]", content);
            Assert.True(snapshot.Metrics.ContainsKey("qwen.prompt.count"));
            Assert.True(snapshot.Metrics.ContainsKey("prompt.count"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_EmitsTelemetryForToolExecution()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-telemetry-tool-{Guid.NewGuid():N}");
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
                  "permissions": {
                    "defaultMode": "default",
                    "allow": ["Read"]
                  },
                  "telemetry": {
                    "enabled": true,
                    "outfile": "telemetry/events.jsonl"
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var telemetry = new TelemetryService(NullLogger<TelemetryService>.Instance);
            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                telemetryService: telemetry);
            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            File.WriteAllText(targetFile, "telemetry content");

            var result = await host.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ExecuteNativeToolRequest
                {
                    ToolName = "read_file",
                    ArgumentsJson = $$"""{"session_id":"session-tool","file_path":"{{targetFile.Replace("\\", "\\\\")}}"}"""
                });

            var snapshot = await telemetry.GetSnapshotAsync(runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot }));
            var content = await File.ReadAllTextAsync(snapshot.EventsPath);

            Assert.Equal("completed", result.Status);
            Assert.Contains("\"event.name\":\"tool_call\"", content);
            Assert.Contains("\"function_name\":\"read_file\"", content);
            Assert.True(snapshot.Metrics.ContainsKey("qwen.tool.call.count"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_EmitsSessionAndPromptTelemetry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-telemetry-session-{Guid.NewGuid():N}");
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
                  "telemetry": {
                    "enabled": true,
                    "outfile": "telemetry/events.jsonl"
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var telemetry = new TelemetryService(NullLogger<TelemetryService>.Instance);
            var sessionHost = TestServiceFactory.CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                telemetryService: telemetry);

            await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Telemetry should record this turn",
                    WorkingDirectory = workspaceRoot
                });

            var snapshot = await telemetry.GetSnapshotAsync(runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot }));
            var content = await File.ReadAllTextAsync(snapshot.EventsPath);

            Assert.Contains("\"event.name\":\"cli_config\"", content);
            Assert.Contains("\"event.name\":\"user_prompt\"", content);
            Assert.True(snapshot.Metrics.ContainsKey("qwen.session.count"));
            Assert.True(snapshot.Metrics.ContainsKey("session.count"));
            Assert.True(snapshot.Metrics.ContainsKey("qwen.prompt.count"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DashScopeAssistantResponseProvider_TryGenerateAsync_EmitsApiRequestTelemetry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-telemetry-provider-{Guid.NewGuid():N}");
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
                  },
                  "telemetry": {
                    "enabled": true,
                    "outfile": "telemetry/events.jsonl"
                  }
                }
                """);

            var runtimeProfile = CreateRuntimeProfile(root, logPrompts: false, workspaceRootOverride: workspaceRoot, homeRootOverride: homeRoot);
            HttpClient httpClient = new(new RecordingHttpMessageHandler((_, _) =>
            {
                var responsePayload = """
                    data: {"choices":[{"delta":{"content":"hello "}}]}

                    data: {"choices":[{"delta":{"content":"world"}}]}

                    data: [DONE]
                    """;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responsePayload, Encoding.UTF8, "text/event-stream")
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
                return Task.FromResult(response);
            }));

            var telemetry = new TelemetryService(NullLogger<TelemetryService>.Instance);
            var provider = new DashScopeAssistantResponseProvider(
                httpClient,
                new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)),
                new TokenLimitService(),
                telemetry);

            var response = await provider.TryGenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "provider-session",
                    Prompt = "Emit telemetry",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "provider-session.jsonl"),
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
                    Provider = "qwen-compatible",
                    ApiKeyEnvironmentVariable = "DASHSCOPE_API_KEY"
                });

            var snapshot = await telemetry.GetSnapshotAsync(runtimeProfile);
            var content = await File.ReadAllTextAsync(snapshot.EventsPath);

            Assert.NotNull(response);
            Assert.Contains("\"event.name\":\"api_request\"", content);
            Assert.True(snapshot.Metrics.ContainsKey("qwen.api.request.count"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static QwenRuntimeProfile CreateRuntimeProfile(
        string root,
        bool logPrompts,
        string? workspaceRootOverride = null,
        string? homeRootOverride = null)
    {
        var workspaceRoot = workspaceRootOverride ?? Path.Combine(root, "workspace");
        var homeRoot = homeRootOverride ?? Path.Combine(root, "home");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        return new QwenRuntimeProfile
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
                LogPrompts = logPrompts
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
    }
}
