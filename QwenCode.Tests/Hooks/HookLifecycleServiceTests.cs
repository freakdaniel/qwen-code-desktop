namespace QwenCode.Tests.Hooks;

public sealed class HookLifecycleServiceTests
{
    [Fact]
    public void HookRegistryService_BuildPlan_LoadsPreToolUseHooks()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-hooks-pretool-{Guid.NewGuid():N}");
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
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                """
                {
                  "hooksConfig": {
                    "enabled": true
                  },
                  "hooks": {
                    "PreToolUse": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "name": "guard-tool",
                            "command": "Write-Output ok"
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfile = new QwenRuntimeProfileService(environmentPaths)
                .Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var registry = new HookRegistryService(environmentPaths);

            var plan = registry.BuildPlan(runtimeProfile, HookEventName.PreToolUse);

            Assert.True(plan.Enabled);
            var hook = Assert.Single(plan.Hooks);
            Assert.Equal(HookEventName.PreToolUse, hook.EventName);
            Assert.Equal("guard-tool", hook.Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
