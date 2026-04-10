using System.Text.Json;
using QwenCode.Core.Extensions;

namespace QwenCode.Tests.Extensions;

public sealed class ExtensionCatalogServiceTests
{
    [Fact]
    public void Inspect_ReturnsInstalledExtensionSurfaces()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ext-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var sourcePath = CreateExtensionSource(root, "demo-extension");

            var snapshot = service.Install(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new InstallExtensionRequest
                {
                    SourcePath = sourcePath,
                    InstallMode = "copy"
                });

            var extension = Assert.Single(snapshot.Extensions);
            Assert.Equal("demo-extension", extension.Name);
            Assert.Equal("1.2.3", extension.Version);
            Assert.True(extension.IsActive);
            Assert.Contains("review:changes", extension.Commands);
            Assert.Contains("quality-skill", extension.Skills);
            Assert.Contains("worker-agent", extension.Agents);
            Assert.Contains("filesystem", extension.McpServers);
            Assert.Contains("telegram", extension.Channels);
            Assert.Equal(2, extension.SettingsCount);
            Assert.Equal(1, extension.HookEventCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Inspect_ReturnsMissingSourceForBrokenLinkedExtension()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ext-link-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var sourcePath = CreateExtensionSource(root, "linked-extension");

            service.Install(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new InstallExtensionRequest
                {
                    SourcePath = sourcePath,
                    InstallMode = "link"
                });

            Directory.Delete(sourcePath, recursive: true);

            var snapshot = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var extension = Assert.Single(snapshot.Extensions);
            Assert.Equal("missing-source", extension.Status);
            Assert.Equal("link", extension.InstallType);
            Assert.False(extension.IsActive);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void SetEnabled_DisablesExtensionForWorkspaceAndRemove_UninstallsWrapper()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ext-toggle-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var sourcePath = CreateExtensionSource(root, "toggle-extension");
            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };

            service.Install(
                workspace,
                new InstallExtensionRequest
                {
                    SourcePath = sourcePath,
                    InstallMode = "copy"
                });

            var disabledSnapshot = service.SetEnabled(
                workspace,
                new SetExtensionEnabledRequest
                {
                    Name = "toggle-extension",
                    Scope = "project",
                    Enabled = false
                });

            var extension = Assert.Single(disabledSnapshot.Extensions);
            Assert.False(extension.WorkspaceEnabled);
            Assert.False(extension.IsActive);

            var emptySnapshot = service.Remove(
                workspace,
                new RemoveExtensionRequest
                {
                    Name = "toggle-extension"
                });

            Assert.Empty(emptySnapshot.Extensions);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetSettings_AndSetSetting_WorkAcrossUserAndWorkspaceScopes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ext-settings-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var sourcePath = CreateExtensionSource(root, "settings-extension");
            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };

            service.Install(
                workspace,
                new InstallExtensionRequest
                {
                    SourcePath = sourcePath,
                    InstallMode = "copy"
                });

            var initialSnapshot = service.GetSettings(
                workspace,
                new GetExtensionSettingsRequest { Name = "settings-extension" });
            var apiKeySetting = Assert.Single(initialSnapshot.Settings, setting => setting.EnvironmentVariable == "API_KEY");
            Assert.True(apiKeySetting.Sensitive);
            Assert.False(apiKeySetting.HasUserValue);
            Assert.False(apiKeySetting.HasWorkspaceValue);

            var userSnapshot = service.SetSetting(
                workspace,
                new SetExtensionSettingValueRequest
                {
                    Name = "settings-extension",
                    Setting = "API_KEY",
                    Scope = "user",
                    Value = "user-secret"
                });
            var userApiKey = Assert.Single(userSnapshot.Settings, setting => setting.EnvironmentVariable == "API_KEY");
            Assert.True(userApiKey.HasUserValue);
            Assert.Equal("user-secret", userApiKey.EffectiveValue);

            var workspaceSnapshot = service.SetSetting(
                workspace,
                new SetExtensionSettingValueRequest
                {
                    Name = "settings-extension",
                    Setting = "API_BASE",
                    Scope = "project",
                    Value = "https://workspace.example"
                });
            var baseSetting = Assert.Single(workspaceSnapshot.Settings, setting => setting.EnvironmentVariable == "API_BASE");
            Assert.True(baseSetting.HasWorkspaceValue);
            Assert.Equal("https://workspace.example", baseSetting.EffectiveValue);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Update_ReinstallsCopiedExtensionFromOriginalSource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ext-update-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var sourcePath = CreateExtensionSource(root, "update-extension");
            var workspace = new WorkspacePaths { WorkspaceRoot = workspaceRoot };

            service.Install(
                workspace,
                new InstallExtensionRequest
                {
                    SourcePath = sourcePath,
                    InstallMode = "copy",
                    SourceType = "local",
                    AutoUpdate = true
                });

            File.WriteAllText(
                Path.Combine(sourcePath, "qwen-extension.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        name = "update-extension",
                        version = "2.0.0",
                        description = "Updated extension"
                    },
                    new JsonSerializerOptions { WriteIndented = true }));

            var updated = service.Update(
                workspace,
                new UpdateExtensionRequest
                {
                    Name = "update-extension"
                });

            var extension = Assert.Single(updated.Extensions);
            Assert.Equal("2.0.0", extension.Version);
            Assert.Equal("local", extension.InstallType);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void CreateScaffold_CreatesTemplateFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ext-scaffold-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var targetPath = Path.Combine(root, "new-extension");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var snapshot = service.CreateScaffold(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new CreateExtensionScaffoldRequest
                {
                    TargetPath = targetPath,
                    Template = "skills"
                });

            Assert.Equal("new-extension", snapshot.Name);
            Assert.Equal("skills", snapshot.Template);
            Assert.True(File.Exists(Path.Combine(targetPath, "qwen-extension.json")));
            Assert.True(File.Exists(Path.Combine(targetPath, "skills", "example-skill", "SKILL.md")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void PreviewConsent_DescribesRisksAndInstalledSurfaces()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ext-consent-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(homeRoot);
            var sourcePath = CreateExtensionSource(root, "consent-extension");

            var consent = service.PreviewConsent(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new InstallExtensionRequest
                {
                    SourcePath = sourcePath,
                    InstallMode = "copy",
                    SourceType = "local"
                });

            Assert.Equal("consent-extension", consent.Name);
            Assert.Contains("review:changes", consent.Commands);
            Assert.Contains("quality-skill", consent.Skills);
            Assert.Contains("worker-agent", consent.Agents);
            Assert.Contains("filesystem", consent.McpServers);
            Assert.Contains("telegram", consent.Channels);
            Assert.Contains(consent.Warnings, static item => item.Contains("unexpected behavior", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(consent.Warnings, static item => item.Contains("MCP", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ExtensionCatalogService CreateService(string homeRoot) =>
        new(
            new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, null, homeRoot, homeRoot)),
            new FakeDesktopEnvironmentPaths(homeRoot, null, homeRoot, homeRoot));

    private static string CreateExtensionSource(string root, string extensionName)
    {
        var sourcePath = Path.Combine(root, $"{extensionName}-src");
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(Path.Combine(sourcePath, "commands", "review"));
        Directory.CreateDirectory(Path.Combine(sourcePath, "skills", "quality-skill"));
        Directory.CreateDirectory(Path.Combine(sourcePath, "agents", "worker-agent"));

        File.WriteAllText(Path.Combine(sourcePath, "commands", "review", "changes.md"), "# Review changes");
        File.WriteAllText(
            Path.Combine(sourcePath, "skills", "quality-skill", "SKILL.md"),
            """
            ---
            name: quality-skill
            ---
            Skill body
            """);
        File.WriteAllText(Path.Combine(sourcePath, "agents", "worker-agent", "AGENT.md"), "# Worker agent");

        var manifest = new
        {
            name = extensionName,
            version = "1.2.3",
            description = "Demo extension",
            contextFileName = new[] { "QWEN.md", "AGENTS.md" },
            commands = new[] { "commands" },
            skills = new[] { "skills" },
            agents = new[] { "agents" },
            settings = new object[]
            {
                new { name = "apiKey", description = "API key", envVar = "API_KEY", sensitive = true },
                new { name = "apiBase", description = "API base", envVar = "API_BASE", sensitive = false }
            },
            hooks = new { PreToolUse = Array.Empty<object>() },
            mcpServers = new { filesystem = new { command = "node server.js" } },
            channels = new { telegram = new { entry = "dist/index.js" } }
        };

        File.WriteAllText(
            Path.Combine(sourcePath, "qwen-extension.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

        return sourcePath;
    }
}
