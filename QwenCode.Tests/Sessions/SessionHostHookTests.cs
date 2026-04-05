namespace QwenCode.Tests.Sessions;

public sealed class SessionHostHookTests
{
    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_BlocksPromptWhenHookDeniesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-hook-block-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            string command;
            if (OperatingSystem.IsWindows())
            {
                var scriptPath = Path.Combine(root, "deny-hook.ps1");
                File.WriteAllText(
                    scriptPath,
                    """
                    [Console]::Error.Write('Blocked by desktop policy hook')
                    exit 2
                    """);
                command = $"& '{scriptPath.Replace("\\", "\\\\", StringComparison.Ordinal)}'";
            }
            else
            {
                command = CrossPlatformTestSupport.CreateHookCommand(
                    root,
                    "deny-hook",
                    string.Empty,
                    """
                    printf '%s' 'Blocked by desktop policy hook' >&2
                    exit 2
                    """);
            }
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                $$"""
                {
                  "hooksConfig": {
                    "enabled": true
                  },
                  "hooks": {
                    "UserPromptSubmit": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "name": "deny-prompt",
                            "command": "{{command}}"
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var hookLifecycleService = new HookLifecycleService(
                new HookRegistryService(environmentPaths),
                new HookCommandRunner(),
                new HookOutputAggregator());
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                userPromptHookService: new UserPromptHookService(hookLifecycleService));

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Run the desktop agent pipeline.",
                    WorkingDirectory = workspaceRoot
                });

            Assert.True(result.CreatedNewSession);
            Assert.Equal("blocked", result.Session.Status);
            Assert.Equal("blocked", result.ToolExecution.Status);
            Assert.Contains("Blocked by desktop policy hook", result.AssistantSummary);
            Assert.True(File.Exists(result.Session.TranscriptPath));

            var transcript = File.ReadAllLines(result.Session.TranscriptPath);
            Assert.Equal(2, transcript.Length);
            Assert.Contains("\"type\":\"user\"", transcript[0]);
            Assert.Contains("\"type\":\"system\"", transcript[1]);
            Assert.DoesNotContain(transcript, line => line.Contains("\"type\":\"assistant\"", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_FiresLifecycleHooksAndAppliesStopReason()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-hook-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var compatibilityService = new QwenCompatibilityService(environmentPaths);
            var recordingHooks = new RecordingHookLifecycleService();
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                hookLifecycleService: recordingHooks);

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Continue the coding session.",
                    WorkingDirectory = workspaceRoot
                });

            Assert.Equal("Stop hook replaced the assistant response.", result.AssistantSummary);
            Assert.Contains(recordingHooks.Events, static item => item == HookEventName.SessionStart);
            Assert.Contains(recordingHooks.Events, static item => item == HookEventName.Stop);
            Assert.Contains(recordingHooks.Events, static item => item == HookEventName.Notification);
            Assert.Contains(recordingHooks.Events, static item => item == HookEventName.SessionEnd);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class RecordingHookLifecycleService : IHookLifecycleService
    {
        public List<HookEventName> Events { get; } = [];

        public Task<HookLifecycleResult> ExecuteAsync(
            QwenRuntimeProfile runtimeProfile,
            HookInvocationRequest request,
            CancellationToken cancellationToken = default)
        {
            Events.Add(request.EventName);

            if (request.EventName == HookEventName.Stop)
            {
                return Task.FromResult(new HookLifecycleResult
                {
                    AggregateOutput = new HookOutput
                    {
                        Continue = false,
                        StopReason = "Stop hook replaced the assistant response.",
                        Decision = "allow"
                    }
                });
            }

            return Task.FromResult(new HookLifecycleResult());
        }
    }
}
