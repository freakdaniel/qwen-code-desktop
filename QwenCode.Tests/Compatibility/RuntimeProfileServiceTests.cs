namespace QwenCode.Tests.Compatibility;

public sealed class RuntimeProfileServiceTests
{
    [Fact]
    public void QwenRuntimeProfileService_Inspect_ResolvesRuntimeOutputAndApprovalRules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-runtime-{Guid.NewGuid():N}");
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
                  "permissions": {
                    "allow": ["Bash(git *)"]
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "advanced": {
                    "runtimeOutputDir": ".qwen-runtime"
                  },
                  "permissions": {
                    "defaultMode": "auto-edit",
                    "confirmShellCommands": true,
                    "confirmFileEdits": false,
                    "ask": ["Edit"],
                    "deny": ["Read(.env)"]
                  },
                  "tools": {
                    "core": ["Read", "Write"]
                  },
                  "context": {
                    "fileName": ["TEAM.md", "QWEN.md"]
                  }
                }
                """);

            var service = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var profile = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.Equal(Path.Combine(workspaceRoot, ".qwen-runtime"), profile.RuntimeBaseDirectory);
            Assert.Equal("project-settings", profile.RuntimeSource);
            Assert.Equal("auto-edit", profile.ApprovalProfile.DefaultMode);
            Assert.True(profile.ApprovalProfile.ConfirmShellCommands);
            Assert.False(profile.ApprovalProfile.ConfirmFileEdits);
            Assert.Contains("Bash(git *)", profile.ApprovalProfile.AllowRules);
            Assert.Contains("Read", profile.ApprovalProfile.AllowRules);
            Assert.Contains("Write", profile.ApprovalProfile.AllowRules);
            Assert.Contains("Edit", profile.ApprovalProfile.AskRules);
            Assert.Contains("Read(.env)", profile.ApprovalProfile.DenyRules);
            Assert.Equal(["TEAM.md", "QWEN.md"], profile.ContextFileNames);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QwenRuntimeProfileService_Inspect_DefaultsContextFilesToQwenAndAgents()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-runtime-default-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            var service = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var profile = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.Equal(["QWEN.md", "AGENTS.md"], profile.ContextFileNames);
            Assert.Equal(
                [
                    Path.Combine(workspaceRoot, "QWEN.md"),
                    Path.Combine(workspaceRoot, "AGENTS.md")
                ],
                profile.ContextFilePaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
