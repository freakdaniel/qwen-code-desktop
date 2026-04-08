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
            string command;
            if (OperatingSystem.IsWindows())
            {
                var scriptPath = Path.Combine(root, "subagent-hook.ps1");
                File.WriteAllText(
                    scriptPath,
                    $$"""
                    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
                    Add-Content -Path '{{hookLogPath}}' -Value "$($payload.hook_event_name)|$($payload.agent_name)"
                    [Console]::Out.Write('{"decision":"allow"}')
                    """);
                command = $"& '{scriptPath.Replace("\\", "\\\\", StringComparison.Ordinal)}'";
            }
            else
            {
                var escapedHookLogPath = hookLogPath.Replace("'", "'\\''", StringComparison.Ordinal);
                command = CrossPlatformTestSupport.CreateHookCommand(
                    root,
                    "subagent-hook",
                    string.Empty,
                    $$"""
                    payload="$(cat)"
                    event_name="$(printf '%s' "$payload" | sed -n 's/.*"hook_event_name":"\([^"]*\)".*/\1/p')"
                    agent_name="$(printf '%s' "$payload" | sed -n 's/.*"agent_name":"\([^"]*\)".*/\1/p')"
                    printf '%s|%s\n' "$event_name" "$agent_name" >> '{{escapedHookLogPath}}'
                    printf '%s' '{"decision":"allow"}'
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
            var modelSelectionService = new SubagentModelSelectionService();
            var validationService = new SubagentValidationService(modelSelectionService);
            var hookLifecycleService = new HookLifecycleService(
                new HookRegistryService(environmentPaths),
                new HookCommandRunner(),
                new HookOutputAggregator());
            var coordinator = new SubagentCoordinatorService(
                new SubagentCatalogService(environmentPaths, validationService),
                new ToolCatalogService(runtimeProfileService, approvalPolicy),
                compatibilityService,
                modelSelectionService,
                validationService,
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

    [Fact]
    public async Task SubagentCoordinatorService_ExecuteAsync_UsesSubagentPromptMode()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-subagent-mode-{Guid.NewGuid():N}");
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
            var approvalPolicy = new ApprovalPolicyService();
            var modelSelectionService = new SubagentModelSelectionService();
            var validationService = new SubagentValidationService(modelSelectionService);
            var capturingRuntime = new CapturingTurnRuntime();
            var coordinator = new SubagentCoordinatorService(
                new SubagentCatalogService(environmentPaths, validationService),
                new ToolCatalogService(runtimeProfileService, approvalPolicy),
                compatibilityService,
                modelSelectionService,
                validationService,
                serviceProvider: new SingleRuntimeServiceProvider(capturingRuntime));
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
            Assert.NotNull(capturingRuntime.LastRequest);
            Assert.Equal(AssistantPromptMode.Subagent, capturingRuntime.LastRequest!.PromptMode);
            Assert.Contains("Role: general-purpose", capturingRuntime.LastRequest.SystemPromptOverride, StringComparison.Ordinal);
            Assert.Contains("Do not address the end user directly", capturingRuntime.LastRequest.SystemPromptOverride, StringComparison.Ordinal);
            Assert.Contains("You are not alone in the codebase", capturingRuntime.LastRequest.SystemPromptOverride, StringComparison.Ordinal);
            Assert.Contains("Execution boundaries:", capturingRuntime.LastRequest.Prompt, StringComparison.Ordinal);
            Assert.Contains("Return contract:", capturingRuntime.LastRequest.Prompt, StringComparison.Ordinal);
            Assert.Contains("If you edit code, include the touched file paths", capturingRuntime.LastRequest.Prompt, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CapturingTurnRuntime : IAssistantTurnRuntime
    {
        public AssistantTurnRequest? LastRequest { get; private set; }

        public Task<AssistantTurnResponse> GenerateAsync(
            AssistantTurnRequest request,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new AssistantTurnResponse
            {
                Summary = "subagent completed",
                ProviderName = "test-subagent",
                Model = "test-model",
                ToolExecutions = []
            });
        }
    }

    private sealed class SingleRuntimeServiceProvider(IAssistantTurnRuntime runtime) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IAssistantTurnRuntime) ? runtime : null;
    }
}
