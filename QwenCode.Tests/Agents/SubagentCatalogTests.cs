namespace QwenCode.Tests.Agents;

public sealed class SubagentCatalogTests
{
    [Fact]
    public void SubagentCatalogService_ListAgents_IncludesBuiltinAndProjectAgents()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-agent-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "agents"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen", "agents"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "agents", "repo-research.md"),
                """
                ---
                name: repo-research
                description: Explore repository-level behavior
                tools:
                  - ReadFile
                  - Lsp
                ---

                You are a repository exploration specialist.
                """);

            var catalog = new SubagentCatalogService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var agents = catalog.ListAgents(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.Contains(agents, agent => agent.Name == "general-purpose" && agent.IsBuiltin);
            Assert.Contains(agents, agent => agent.Name == "Explore" && agent.IsBuiltin);
            var projectAgent = Assert.Single(agents, agent => agent.Name == "repo-research");
            Assert.Equal("project", projectAgent.Scope);
            Assert.Contains("Lsp", projectAgent.Tools);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SubagentCatalogService_ListAgents_ProjectScopeOverridesUserScope()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-agent-override-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "agents"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen", "agents"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "agents", "custom.md"),
                """
                ---
                name: custom
                description: user version
                ---

                User agent
                """);
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "agents", "custom.md"),
                """
                ---
                name: custom
                description: project version
                ---

                Project agent
                """);

            var catalog = new SubagentCatalogService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var agent = catalog.FindAgent(new WorkspacePaths { WorkspaceRoot = workspaceRoot }, "custom");

            Assert.NotNull(agent);
            Assert.Equal("project", agent!.Scope);
            Assert.Equal("project version", agent.Description);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
