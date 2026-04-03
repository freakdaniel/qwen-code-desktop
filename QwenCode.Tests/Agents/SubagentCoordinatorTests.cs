using System.Text.Json;

namespace QwenCode.Tests.Agents;

public sealed class SubagentCoordinatorTests
{
    [Fact]
    public async Task SubagentCoordinatorService_ExecuteAsync_RunsSubagentStartAndStopHooks()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-subagent-hooks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            var hookLogPath = Path.Combine(root, "subagent-hooks.log");
            var scriptPath = Path.Combine(root, "subagent-hook.ps1");
            File.WriteAllText(
                scriptPath,
                $$"""
                $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
                Add-Content -Path '{{hookLogPath}}' -Value "$($payload.hook_event_name)|$($payload.agent_name)"
                [Console]::Out.Write('{"decision":"allow"}')
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
                    "SubagentStart": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "name": "subagent-start",
                            "command": "{{command}}"
                          }
                        ]
                      }
                    ],
                    "SubagentStop": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "name": "subagent-stop",
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
            var approvalPolicy = new ApprovalPolicyService();
            var hookLifecycleService = new HookLifecycleService(
                new HookRegistryService(environmentPaths),
                new HookCommandRunner(),
                new HookOutputAggregator());
            var coordinator = new SubagentCoordinatorService(
                new SubagentCatalogService(environmentPaths),
                new ToolCatalogService(runtimeProfileService, approvalPolicy),
                compatibilityService,
                hookLifecycleService);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            using var arguments = JsonDocument.Parse(
                """
                {
                  "description": "Inspect the repository",
                  "prompt": "Summarize the current runtime status",
                  "subagent_type": "general-purpose"
                }
                """);

            var result = await coordinator.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                runtimeProfile,
                arguments.RootElement,
                "allow");

            Assert.Equal("completed", result.Status);
            Assert.True(File.Exists(hookLogPath));
            var log = await File.ReadAllLinesAsync(hookLogPath);
            Assert.Contains(log, line => line.StartsWith("SubagentStart|", StringComparison.Ordinal));
            Assert.Contains(log, line => line.StartsWith("SubagentStop|", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
