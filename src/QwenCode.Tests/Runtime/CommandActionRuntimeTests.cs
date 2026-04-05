namespace QwenCode.Tests.Runtime;

public sealed class CommandActionRuntimeTests
{
    [Fact]
    public async Task CommandActionRuntime_TryInvokeAsync_ExecutesMemoryCommands()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-command-actions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(Path.Combine(workspaceRoot, "QWEN.md"), "# Project memory");
            File.WriteAllText(Path.Combine(homeRoot, ".qwen", "QWEN.md"), "# Global memory");

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var toolRegistry = new ToolCatalogService(runtimeProfileService, new ApprovalPolicyService());
            var runtime = new CommandActionRuntime(
                new SlashCommandRuntime(compatibilityService),
                runtimeProfileService,
                compatibilityService,
                toolRegistry);

            var showResult = await runtime.TryInvokeAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "/memory show",
                workspaceRoot);

            var addResult = await runtime.TryInvokeAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "/memory add --project remember desktop port parity",
                workspaceRoot);

            var refreshResult = await runtime.TryInvokeAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "/memory refresh",
                workspaceRoot);

            Assert.NotNull(showResult);
            Assert.Equal("completed", showResult!.Status);
            Assert.Contains("Project memory", showResult.Output);
            Assert.Contains("Global memory", showResult.Output);

            Assert.NotNull(addResult);
            Assert.Equal("completed", addResult!.Status);
            Assert.Contains("Saved memory", addResult.Output);
            Assert.Contains("remember desktop port parity", File.ReadAllText(Path.Combine(workspaceRoot, "QWEN.md")));

            Assert.NotNull(refreshResult);
            Assert.Equal("completed", refreshResult!.Status);
            Assert.Contains("Memory refreshed successfully", refreshResult.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CommandActionRuntime_TryInvokeAsync_ExecutesContextCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-context-command-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "commands", "qc"));
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "skills", "project-review"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(Path.Combine(workspaceRoot, "QWEN.md"), "# Project memory");
            File.WriteAllText(Path.Combine(homeRoot, ".qwen", "QWEN.md"), "# Global memory");
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "commands", "qc", "code-review.md"),
                """
                ---
                description: Code review a pull request
                ---
                """
            );
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "skills", "project-review", "SKILL.md"),
                """
                ---
                name: project-review
                description: Review project changes with local context
                ---
                """
            );

            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var runtime = new CommandActionRuntime(
                new SlashCommandRuntime(compatibilityService),
                runtimeProfileService,
                compatibilityService,
                new ToolCatalogService(runtimeProfileService, new ApprovalPolicyService()));

            var result = await runtime.TryInvokeAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                "/context detail",
                workspaceRoot);

            Assert.NotNull(result);
            Assert.Equal("completed", result!.Status);
            Assert.Equal("context", result.Command.Name);
            Assert.Contains("Workspace:", result.Output);
            Assert.Contains("Slash commands: 1", result.Output);
            Assert.Contains("Skills: 1", result.Output);
            Assert.Contains("qc/code-review", result.Output);
            Assert.Contains("project-review", result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
