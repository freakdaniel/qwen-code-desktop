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

            var scriptPath = Path.Combine(root, "deny-hook.ps1");
            File.WriteAllText(
                scriptPath,
                """
                [Console]::Error.Write('Blocked by desktop policy hook')
                exit 2
                """);

            var command = $"& '{scriptPath.Replace("\\", "\\\\", StringComparison.Ordinal)}'";
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
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                userPromptHookService: new UserPromptHookService(
                    new HookRegistryService(environmentPaths),
                    new HookCommandRunner(),
                    new HookOutputAggregator()));

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
}
