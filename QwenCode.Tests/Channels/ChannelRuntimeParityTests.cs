using System.Text.Json;
using QwenCode.App.Channels;
using QwenCode.App.Extensions;
using QwenCode.App.Sessions;

namespace QwenCode.Tests.Channels;

public sealed class ChannelRuntimeParityTests
{
    [Fact]
    public async Task ChannelRuntimeService_HandleInboundAsync_BlocksUnmentionedGroupMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-group-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (workspaceRoot, runtime) = CreateRuntime(
                root,
                """
                {
                  "channels": {
                    "ops": {
                      "type": "telegram",
                      "senderPolicy": "open",
                      "sessionScope": "user",
                      "groupPolicy": "open",
                      "groups": {
                        "*": { "requireMention": true }
                      },
                      "cwd": "."
                    }
                  }
                }
                """);

            var result = await runtime.HandleInboundAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "ops",
                JsonDocument.Parse("""{"senderId":"user-1","senderName":"Alice","chatId":"group-1","text":"hello","isGroup":true}""").RootElement);

            Assert.Equal("blocked", result.Status);
            Assert.Contains("mention", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChannelRuntimeService_HandleInboundAsync_HandlesHelpAndClearCommands()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-command-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (workspaceRoot, runtime) = CreateRuntime(
                root,
                """
                {
                  "channels": {
                    "ops": {
                      "type": "telegram",
                      "senderPolicy": "open",
                      "sessionScope": "user",
                      "dispatchMode": "followup",
                      "cwd": "."
                    }
                  }
                }
                """);

            var help = await runtime.HandleInboundAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "ops",
                JsonDocument.Parse("""{"senderId":"user-1","senderName":"Alice","chatId":"chat-1","text":"/help"}""").RootElement);
            var firstDispatch = await runtime.HandleInboundAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "ops",
                JsonDocument.Parse("""{"senderId":"user-1","senderName":"Alice","chatId":"chat-1","text":"hello"}""").RootElement);
            var clear = await runtime.HandleInboundAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "ops",
                JsonDocument.Parse("""{"senderId":"user-1","senderName":"Alice","chatId":"chat-1","text":"/clear"}""").RootElement);

            Assert.Equal("local-command", help.Status);
            Assert.Contains("/status", help.Message);
            Assert.Equal("dispatched", firstDispatch.Status);
            Assert.Equal("local-command", clear.Status);
            Assert.Contains("Session cleared", clear.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ChannelRegistryService_GetRuntimeConfiguration_DefaultsDispatchModeToCollect()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-collect-default-{Guid.NewGuid():N}");
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

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var settingsResolver = new DesktopSettingsResolver(compatibilityService, runtimeProfileService);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var registry = new ChannelRegistryService(environmentPaths, settingsResolver, extensionCatalog);

            var configuration = registry.GetRuntimeConfiguration(new WorkspacePaths { WorkspaceRoot = workspaceRoot }, "ops");

            Assert.Equal("collect", configuration.DispatchMode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChannelRuntimeService_HandleInboundAsync_IncludesAttachmentContextInPrompt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-channel-attachments-{Guid.NewGuid():N}");
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

            var sessionHost = new RecordingSessionHost();
            var runtime = CreateRuntime(workspaceRoot, homeRoot, systemRoot, sessionHost);

            _ = await runtime.HandleInboundAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "ops",
                JsonDocument.Parse(
                    """
                    {
                      "senderId":"user-1",
                      "senderName":"Alice",
                      "chatId":"chat-1",
                      "text":"please review",
                      "imageBase64":"abcd",
                      "imageMimeType":"image/png",
                      "attachments":[
                        {"type":"file","fileName":"spec.txt","filePath":"D:\\docs\\spec.txt","mimeType":"text/plain"}
                      ]
                    }
                    """).RootElement);

            Assert.Contains("Embedded image attached", sessionHost.LastPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Attachments:", sessionHost.LastPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("spec.txt", sessionHost.LastPrompt, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (string WorkspaceRoot, ChannelRuntimeService Runtime) CreateRuntime(string root, string settingsJson)
    {
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);
        File.WriteAllText(Path.Combine(workspaceRoot, ".qwen", "settings.json"), settingsJson);

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
        var pluginRuntime = new ChannelPluginRuntimeService(
            new ChannelPluginRegistryService(extensionCatalog),
            registry,
            sessionHost);

        return (workspaceRoot, new ChannelRuntimeService(
            registry,
            pluginRuntime,
            adapters,
            router,
            environmentPaths,
            deliveryService,
            sessionHost));
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

    private sealed class RecordingSessionHost : ISessionHost
    {
        public event EventHandler<DesktopSessionEvent>? SessionEvent;

        public string LastPrompt { get; private set; } = string.Empty;

        public Task<DesktopSessionTurnResult> StartTurnAsync(WorkspacePaths paths, StartDesktopSessionTurnRequest request, CancellationToken cancellationToken = default)
        {
            LastPrompt = request.Prompt;
            return Task.FromResult(CreateResult(request));
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

        private static DesktopSessionTurnResult CreateResult(StartDesktopSessionTurnRequest request) =>
            new()
            {
                Session = CreateSessionPreview(request),
                AssistantSummary = "ok",
                CreatedNewSession = true,
                ToolExecution = CreateToolExecution(request)
            };
    }

    private static SessionPreview CreateSessionPreview(StartDesktopSessionTurnRequest request) =>
        new()
        {
            SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString() : request.SessionId,
            Title = "channel session",
            LastActivity = "just now",
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            LastUpdatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Category = "channel",
            Mode = DesktopMode.Code,
            Status = "ready",
            WorkingDirectory = request.WorkingDirectory,
            GitBranch = "main",
            TranscriptPath = Path.Combine(request.WorkingDirectory, ".qwen", "session.jsonl"),
            MetadataPath = Path.Combine(request.WorkingDirectory, ".qwen", "session.meta.json")
        };

    private static NativeToolExecutionResult CreateToolExecution(StartDesktopSessionTurnRequest request) =>
        new()
        {
            ToolName = request.ToolName,
            Status = "not-requested",
            ApprovalState = "allow",
            WorkingDirectory = request.WorkingDirectory,
            ChangedFiles = []
        };
}
