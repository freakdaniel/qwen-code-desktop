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

    [Fact]
    public void HookRegistryService_BuildPlan_FiltersHooksByMatcher()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-hooks-matcher-{Guid.NewGuid():N}");
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
                        "matcher": "^grep_.*$",
                        "hooks": [
                          {
                            "type": "command",
                            "name": "grep-guard",
                            "command": "Write-Output ok"
                          }
                        ]
                      },
                      {
                        "matcher": "^read_file$",
                        "hooks": [
                          {
                            "type": "command",
                            "name": "reader-guard",
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

            var plan = registry.BuildPlan(
                runtimeProfile,
                new HookInvocationRequest
                {
                    EventName = HookEventName.PreToolUse,
                    SessionId = "session-1",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = string.Empty,
                    ToolName = "grep_search"
                });

            Assert.True(plan.Enabled);
            var hook = Assert.Single(plan.Hooks);
            Assert.Equal("grep-guard", hook.Name);
            Assert.Equal("^grep_.*$", hook.Matcher);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HookRegistryService_BuildPlan_LoadsHooksFromActiveExtensions()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-hooks-extension-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var extensionSourceRoot = Path.Combine(root, "ext-source");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);
            Directory.CreateDirectory(extensionSourceRoot);

            File.WriteAllText(
                Path.Combine(extensionSourceRoot, "qwen-extension.json"),
                """
                {
                  "name": "hooked-ext",
                  "version": "1.0.0",
                  "hooks": {
                    "PreToolUse": [
                      {
                        "matcher": "^grep_.*$",
                        "hooks": [
                          {
                            "type": "command",
                            "name": "extension-grep-guard",
                            "command": "Write-Output ok"
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var extensionCatalog = new ExtensionCatalogService(runtimeProfileService, environmentPaths);
            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            _ = extensionCatalog.Install(
                workspace,
                new InstallExtensionRequest
                {
                    SourcePath = extensionSourceRoot,
                    InstallMode = "link"
                });
            _ = extensionCatalog.SetEnabled(
                workspace,
                new SetExtensionEnabledRequest
                {
                    Name = "hooked-ext",
                    Scope = "project",
                    Enabled = true
                });

            var runtimeProfile = runtimeProfileService.Inspect(workspace);
            var registry = new HookRegistryService(environmentPaths, extensionCatalog);

            var plan = registry.BuildPlan(
                runtimeProfile,
                new HookInvocationRequest
                {
                    EventName = HookEventName.PreToolUse,
                    SessionId = "session-ext",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = string.Empty,
                    ToolName = "grep_search"
                });

            Assert.True(plan.Enabled);
            var hook = Assert.Single(plan.Hooks);
            Assert.Equal("extension-grep-guard", hook.Name);
            Assert.Equal(HookConfigSource.Extensions, hook.Source);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
