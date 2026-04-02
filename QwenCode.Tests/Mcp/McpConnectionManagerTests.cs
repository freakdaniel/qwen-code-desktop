namespace QwenCode.Tests.Mcp;

public sealed class McpConnectionManagerTests
{
    [Fact]
    public async Task McpConnectionManagerService_ReconnectAsync_ConnectsToAvailableStdioCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-connect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var toolRoot = Path.Combine(root, "tools");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);
            Directory.CreateDirectory(toolRoot);

            var scriptPath = Path.Combine(toolRoot, OperatingSystem.IsWindows() ? "demo.cmd" : "demo");
            await File.WriteAllTextAsync(scriptPath, OperatingSystem.IsWindows() ? "@echo off\r\necho demo" : "#!/bin/sh\necho demo");

            var paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var environment = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var registry = new McpRegistryService(
                new QwenRuntimeProfileService(environment),
                new FileMcpTokenStore(environment));
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "local-demo",
                Scope = "user",
                Transport = "stdio",
                CommandOrUrl = scriptPath
            });

            var manager = new McpConnectionManagerService(registry, new HttpClient());
            var result = await manager.ReconnectAsync(paths, "local-demo");
            var listed = Assert.Single(manager.ListServersWithStatus(paths));

            Assert.Equal("connected", result.Status);
            Assert.Equal("connected", listed.Status);
            Assert.NotNull(listed.LastReconnectAttemptUtc);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task McpConnectionManagerService_ReconnectAsync_ReturnsDisconnectedForInvalidHttpEndpoint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-disconnect-{Guid.NewGuid():N}");
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
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "broken-http",
                Scope = "user",
                Transport = "http",
                CommandOrUrl = "https://127.0.0.1:1/mcp"
            });

            var manager = new McpConnectionManagerService(registry, new HttpClient());
            var result = await manager.ReconnectAsync(paths, "broken-http");

            Assert.Equal("disconnected", result.Status);
            Assert.NotEmpty(result.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
