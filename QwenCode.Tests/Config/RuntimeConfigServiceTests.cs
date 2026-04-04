using QwenCode.App.Config;

namespace QwenCode.Tests.Config;

public sealed class RuntimeConfigServiceTests
{
    [Fact]
    public void Inspect_MergesSettingsLayersAndParsesRuntimeConfigSurface()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var userQwenRoot = Path.Combine(homeRoot, ".qwen");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(userQwenRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(systemRoot, "system-defaults.json"),
                """
                {
                  "telemetry": {
                    "enabled": false,
                    "target": "local"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(userQwenRoot, "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "openai"
                    }
                  },
                  "model": {
                    "name": "qwen-max"
                  },
                  "modelProviders": {
                    "openai": [
                      {
                        "id": "qwen-max",
                        "baseUrl": "https://provider.example/v1",
                        "envKey": "CUSTOM_OPENAI_KEY"
                      }
                    ]
                  },
                  "telemetry": {
                    "enabled": true,
                    "target": "local",
                    "outfile": "telemetry.ndjson"
                  },
                  "env": {
                    "CUSTOM_OPENAI_KEY": "from-settings"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "embeddingModel": "text-embedding-v4",
                  "chatCompression": {
                    "contextPercentageThreshold": 0.72
                  },
                  "allowedMcpServers": ["docs", "github"],
                  "excludedMcpServers": ["unsafe-server"],
                  "overrideExtensions": ["org.qwen.desktop"],
                  "disableAllHooks": true,
                  "checkpointing": true
                }
                """);

            var service = new RuntimeConfigService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var snapshot = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.Equal("openai", snapshot.SelectedAuthType);
            Assert.Equal("qwen-max", snapshot.ModelName);
            Assert.Equal("text-embedding-v4", snapshot.EmbeddingModel);
            Assert.Single(snapshot.ModelProviders);
            Assert.Equal("CUSTOM_OPENAI_KEY", snapshot.ModelProviders[0].EnvironmentVariableName);
            Assert.Equal(0.72d, snapshot.ChatCompression?.ContextPercentageThreshold);
            Assert.True(snapshot.Telemetry?.Enabled);
            Assert.Equal("local", snapshot.Telemetry?.Target);
            Assert.Equal("telemetry.ndjson", snapshot.Telemetry?.Outfile);
            Assert.True(snapshot.DisableAllHooks);
            Assert.True(snapshot.Checkpointing);
            Assert.Equal(["docs", "github"], snapshot.AllowedMcpServers);
            Assert.Equal(["unsafe-server"], snapshot.ExcludedMcpServers);
            Assert.Equal(["org.qwen.desktop"], snapshot.OverrideExtensions);
            Assert.Equal("from-settings", snapshot.Environment["CUSTOM_OPENAI_KEY"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Inspect_DoesNotMergeProjectLayerForUntrustedWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-config-untrusted-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var userQwenRoot = Path.Combine(homeRoot, ".qwen");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(userQwenRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(userQwenRoot, "settings.json"),
                """
                {
                  "security": {
                    "folderTrust": {
                      "enabled": true
                    }
                  },
                  "model": {
                    "name": "qwen3-coder-plus"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(userQwenRoot, "trustedFolders.json"),
                $$"""
                {
                  "{{workspaceRoot.Replace("\\", "\\\\", StringComparison.Ordinal)}}": "DO_NOT_TRUST"
                }
                """);
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "model": {
                    "name": "project-model-should-not-apply"
                  },
                  "disableAllHooks": true
                }
                """);

            var service = new RuntimeConfigService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var snapshot = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.True(snapshot.FolderTrustEnabled);
            Assert.False(snapshot.IsWorkspaceTrusted);
            Assert.Equal("file", snapshot.WorkspaceTrustSource);
            Assert.Equal("qwen3-coder-plus", snapshot.ModelName);
            Assert.False(snapshot.DisableAllHooks);
            Assert.DoesNotContain(snapshot.SettingsLayers, layer => layer.Scope == "project" && layer.Included);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveSettingsPath_UsesRequestedScope()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-config-scope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            var service = new RuntimeConfigService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };

            Assert.Equal(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                service.ResolveSettingsPath(workspace, "project"));
            Assert.Equal(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                service.ResolveSettingsPath(workspace, "user"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
