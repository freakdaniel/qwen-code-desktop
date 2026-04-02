namespace QwenCode.Tests.Mcp;

public sealed class McpRegistryTests
{
    [Fact]
    public async Task McpRegistryService_AddServer_WritesUserSettingsAndListsServer()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var environment = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var registry = new McpRegistryService(
                new QwenRuntimeProfileService(environment),
                new FileMcpTokenStore(environment));

            var server = registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "docs",
                Scope = "user",
                Transport = "stdio",
                CommandOrUrl = "node",
                Arguments = ["server.js"],
                EnvironmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["API_KEY"] = "test"
                },
                Description = "Docs server",
                IncludeTools = ["fetch-docs"]
            });

            var settingsPath = Path.Combine(homeRoot, ".qwen", "settings.json");
            var settingsContent = await File.ReadAllTextAsync(settingsPath);
            var listedServer = Assert.Single(registry.ListServers(paths));

            Assert.Equal("docs", server.Name);
            Assert.Contains("\"mcpServers\"", settingsContent);
            Assert.Contains("\"command\": \"node\"", settingsContent);
            Assert.Equal("docs", listedServer.Name);
            Assert.Equal("user", listedServer.Scope);
            Assert.Equal("stdio", listedServer.Transport);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task McpRegistryService_ListServers_PrefersProjectScopeOverUserScope()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-registry-override-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            await File.WriteAllTextAsync(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                """
                {
                  "mcpServers": {
                    "shared": {
                      "command": "node",
                      "args": ["user-server.js"]
                    }
                  }
                }
                """);

            await File.WriteAllTextAsync(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "mcpServers": {
                    "shared": {
                      "command": "dotnet",
                      "args": ["project-server.dll"]
                    }
                  }
                }
                """);

            var paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var environment = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var registry = new McpRegistryService(
                new QwenRuntimeProfileService(environment),
                new FileMcpTokenStore(environment));

            var server = Assert.Single(registry.ListServers(paths));

            Assert.Equal("shared", server.Name);
            Assert.Equal("project", server.Scope);
            Assert.Equal("dotnet", server.CommandOrUrl);
            Assert.Contains("project-server.dll", server.Arguments);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task McpRegistryService_RemoveServer_RemovesPersistedToken()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-registry-remove-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var environment = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var tokenStore = new FileMcpTokenStore(environment);
            var registry = new McpRegistryService(
                new QwenRuntimeProfileService(environment),
                tokenStore);

            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "search",
                Scope = "user",
                Transport = "http",
                CommandOrUrl = "https://example.com/mcp"
            });

            await tokenStore.SaveTokenAsync("search", """{"access_token":"abc"}""");
            var removed = registry.RemoveServer(paths, "search", "user");
            var settingsContent = await File.ReadAllTextAsync(Path.Combine(homeRoot, ".qwen", "settings.json"));

            Assert.True(removed);
            Assert.DoesNotContain("\"search\"", settingsContent);
            Assert.False(tokenStore.HasToken("search"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
