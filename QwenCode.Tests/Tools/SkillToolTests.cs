namespace QwenCode.Tests.Tools;

public sealed class SkillToolTests
{
    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_Skill_LoadsProjectSkillBody()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-skill-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var skillRoot = Path.Combine(workspaceRoot, ".qwen", "skills", "code-review");

            Directory.CreateDirectory(skillRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            await File.WriteAllTextAsync(
                Path.Combine(skillRoot, "SKILL.md"),
                """
                ---
                name: code-review
                description: Review code with sharp attention to bugs
                allowedTools:
                  - ReadFile
                  - Lsp
                ---

                Review the changed code for behavioral regressions and missing tests.
                """);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                skillToolService: new SkillToolService(compatibilityService));

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "skill",
                ArgumentsJson = """{"skill":"code-review"}"""
            });

            Assert.Equal("completed", result.Status);
            Assert.Equal("skill", result.ToolName);
            Assert.Contains($"Base directory for this skill: {skillRoot}", result.Output);
            Assert.Contains("Important: ALWAYS resolve absolute paths", result.Output);
            Assert.Contains("Allowed tools: ReadFile, Lsp", result.Output);
            Assert.Contains("Review the changed code for behavioral regressions", result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_Skill_ReturnsErrorForUnknownSkill()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-skill-tool-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var userSkillRoot = Path.Combine(homeRoot, ".qwen", "skills", "testing");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(userSkillRoot);
            Directory.CreateDirectory(systemRoot);

            await File.WriteAllTextAsync(
                Path.Combine(userSkillRoot, "SKILL.md"),
                """
                ---
                name: testing
                description: Help write tests
                ---

                Write focused tests for behavior changes.
                """);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                skillToolService: new SkillToolService(compatibilityService));

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "skill",
                ArgumentsJson = """{"skill":"missing-skill"}"""
            });

            Assert.Equal("error", result.Status);
            Assert.Contains("Available skills: testing", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_Skill_HidesProjectSkillInUntrustedWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-skill-tool-untrusted-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var skillRoot = Path.Combine(workspaceRoot, ".qwen", "skills", "code-review");

            Directory.CreateDirectory(skillRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            await File.WriteAllTextAsync(
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
            await File.WriteAllTextAsync(
                Path.Combine(homeRoot, ".qwen", "trustedFolders.json"),
                BuildTrustedFoldersJson(workspaceRoot, "DO_NOT_TRUST"));
            await File.WriteAllTextAsync(
                Path.Combine(skillRoot, "SKILL.md"),
                """
                ---
                name: code-review
                ---

                Review the changed code for behavioral regressions and missing tests.
                """);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                skillToolService: new SkillToolService(compatibilityService));

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "skill",
                ArgumentsJson = """{"skill":"code-review"}"""
            });

            Assert.Equal("error", result.Status);
            Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Available skills", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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
