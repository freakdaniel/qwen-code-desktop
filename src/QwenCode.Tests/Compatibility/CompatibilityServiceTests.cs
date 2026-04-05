namespace QwenCode.Tests.Compatibility;

public sealed class CompatibilityServiceTests
{
    [Fact]
    public void QwenCompatibilityService_Inspect_ReturnsProjectAndUserLayers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-compat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "commands"));
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "skills"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen", "skills"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """{ "general": {}, "ui": {} }""");
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                """{ "privacy": {} }""");
            File.WriteAllText(Path.Combine(workspaceRoot, "QWEN.md"), "# project memory");

            var previousDefaults = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_DEFAULTS_PATH");
            var previousSettings = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH");

            try
            {
                Environment.SetEnvironmentVariable(
                    "QWEN_CODE_SYSTEM_DEFAULTS_PATH",
                    Path.Combine(systemRoot, "system-defaults.json"));
                Environment.SetEnvironmentVariable(
                    "QWEN_CODE_SYSTEM_SETTINGS_PATH",
                    Path.Combine(systemRoot, "settings.json"));

                var service = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
                var snapshot = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

                Assert.Equal("QWEN.md", snapshot.DefaultContextFileName);
                Assert.Contains(snapshot.SettingsLayers, layer => layer.Id == "project-settings" && layer.Exists);
                Assert.Contains(snapshot.SettingsLayers, layer => layer.Id == "user-settings" && layer.Exists);
                Assert.Contains(snapshot.SurfaceDirectories, surface => surface.Id == "project-commands" && surface.Exists);
                Assert.Contains(snapshot.SurfaceDirectories, surface => surface.Id == "context-root" && surface.Exists);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QWEN_CODE_SYSTEM_DEFAULTS_PATH", previousDefaults);
                Environment.SetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH", previousSettings);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QwenCompatibilityService_Inspect_HidesProjectCommandsAndSkillsInUntrustedWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-compat-untrusted-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "commands"));
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "skills", "project-skill"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen", "commands"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen", "skills", "user-skill"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "folderTrust": {
                      "enabled": true
                    }
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "trustedFolders.json"),
                BuildTrustedFoldersJson(workspaceRoot, "DO_NOT_TRUST"));
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "commands", "project-command.md"),
                "# project command");
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "commands", "user-command.md"),
                "# user command");
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "skills", "project-skill", "SKILL.md"),
                "# project skill");
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "skills", "user-skill", "SKILL.md"),
                "# user skill");

            var service = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var snapshot = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.DoesNotContain(snapshot.Commands, command => command.Scope == "project");
            Assert.DoesNotContain(snapshot.Skills, skill => skill.Scope == "project");
            Assert.Contains(snapshot.Commands, command => command.Scope == "user");
            Assert.Contains(snapshot.Skills, skill => skill.Scope == "user");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string BuildTrustedFoldersJson(string workspaceRoot, string trustValue) =>
        $$"""
        {
          "{{workspaceRoot.Replace("\\", "\\\\", StringComparison.Ordinal)}}": "{{trustValue}}"
        }
        """;
}
