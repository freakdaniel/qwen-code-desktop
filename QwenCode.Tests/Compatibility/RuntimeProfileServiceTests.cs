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
                  "checkpointing": true,
                  "chatCompression": {
                    "contextPercentageThreshold": 0.61
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
            Assert.True(profile.Checkpointing);
            Assert.Equal(0.61d, profile.ChatCompression?.ContextPercentageThreshold);
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

    [Fact]
    public void QwenRuntimeProfileService_Inspect_ResolvesWorkspaceTrustFromTrustedFoldersFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-runtime-trust-{Guid.NewGuid():N}");
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

            var service = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var profile = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.True(profile.FolderTrustEnabled);
            Assert.False(profile.IsWorkspaceTrusted);
            Assert.Equal("file", profile.WorkspaceTrustSource);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QwenRuntimeProfileService_Inspect_DoesNotMergeProjectSettingsInUntrustedWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-runtime-untrusted-settings-{Guid.NewGuid():N}");
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
                  "security": {
                    "folderTrust": {
                      "enabled": true
                    }
                  },
                  "permissions": {
                    "defaultMode": "default"
                  },
                  "context": {
                    "fileName": ["QWEN.md", "AGENTS.md"]
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
                    "defaultMode": "auto-edit"
                  },
                  "context": {
                    "fileName": ["TEAM.md"]
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "trustedFolders.json"),
                BuildTrustedFoldersJson(workspaceRoot, "DO_NOT_TRUST"));

            var service = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var profile = service.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.True(profile.FolderTrustEnabled);
            Assert.False(profile.IsWorkspaceTrusted);
            Assert.Equal("default", profile.ApprovalProfile.DefaultMode);
            Assert.DoesNotContain(".qwen-runtime", profile.RuntimeBaseDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(["QWEN.md", "AGENTS.md"], profile.ContextFileNames);
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
