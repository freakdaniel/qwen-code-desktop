using QwenCode.Core.Mcp;

namespace QwenCode.Tests.Mcp;

public sealed class McpResourceRegistryTests
{
    [Fact]
    public async Task McpResourceRegistryService_GetSnapshotAsync_DeduplicatesResourceNamesAcrossServers()
    {
        var paths = new WorkspacePaths
        {
            WorkspaceRoot = Path.Combine(Path.GetTempPath(), $"qwen-resource-registry-{Guid.NewGuid():N}")
        };
        var connectionManager = new FakeMcpConnectionManager(
            new McpServerDefinition
            {
                Name = "docs-a",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = "http://docs-a",
                Status = "connected"
            },
            new McpServerDefinition
            {
                Name = "docs-b",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = "http://docs-b",
                Status = "connected"
            });
        var runtime = new FakeMcpToolRuntime(
            new Dictionary<string, IReadOnlyList<McpResourceDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                ["docs-a"] =
                [
                    new McpResourceDefinition
                    {
                        ServerName = "docs-a",
                        Name = "README",
                        Uri = "file://README.md",
                        Description = "README A"
                    }
                ],
                ["docs-b"] =
                [
                    new McpResourceDefinition
                    {
                        ServerName = "docs-b",
                        Name = "README",
                        Uri = "file://README.md",
                        Description = "README B"
                    }
                ]
            });

        var service = new McpResourceRegistryService(connectionManager, runtime);

        var snapshot = await service.GetSnapshotAsync(paths, new GetMcpResourceRegistryRequest());

        Assert.Equal(2, snapshot.TotalCount);
        Assert.Equal(2, snapshot.ServerCount);
        Assert.Contains(snapshot.Resources, item => item.Name == "docs-a_README");
        Assert.Contains(snapshot.Resources, item => item.Name == "docs-b_README");
        Assert.All(snapshot.Resources, item => Assert.Equal(item.QualifiedName, $"{item.ServerName}/{item.Uri}"));
    }

    [Fact]
    public async Task McpResourceRegistryService_ReadAsync_ResolvesRenamedResourceAndCallsRuntime()
    {
        var paths = new WorkspacePaths
        {
            WorkspaceRoot = Path.Combine(Path.GetTempPath(), $"qwen-resource-read-{Guid.NewGuid():N}")
        };
        var connectionManager = new FakeMcpConnectionManager(
            new McpServerDefinition
            {
                Name = "docs-a",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = "http://docs-a",
                Status = "connected"
            },
            new McpServerDefinition
            {
                Name = "docs-b",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = "http://docs-b",
                Status = "connected"
            });
        var runtime = new FakeMcpToolRuntime(
            new Dictionary<string, IReadOnlyList<McpResourceDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                ["docs-a"] =
                [
                    new McpResourceDefinition
                    {
                        ServerName = "docs-a",
                        Name = "README",
                        Uri = "file://README.md"
                    }
                ],
                ["docs-b"] =
                [
                    new McpResourceDefinition
                    {
                        ServerName = "docs-b",
                        Name = "README",
                        Uri = "file://README.md"
                    }
                ]
            });

        var service = new McpResourceRegistryService(connectionManager, runtime);
        var snapshot = await service.GetSnapshotAsync(paths, new GetMcpResourceRegistryRequest());
        var renamed = snapshot.Resources.Single(item => string.Equals(item.Name, "docs-b_README", StringComparison.Ordinal));

        var result = await service.ReadAsync(paths, new ReadMcpResourceRegistryEntryRequest
        {
            Name = renamed.Name
        });

        Assert.Equal("docs-b", result.ServerName);
        Assert.Equal("file://README.md", result.Uri);
        Assert.Equal("resource docs-b/file://README.md", result.Output);
    }

    private sealed class FakeMcpConnectionManager(params McpServerDefinition[] servers) : IMcpConnectionManager
    {
        private readonly IReadOnlyList<McpServerDefinition> configuredServers = servers;

        public IReadOnlyList<McpServerDefinition> ListServersWithStatus(WorkspacePaths paths) => configuredServers;

        public Task<McpReconnectResult> ReconnectAsync(WorkspacePaths paths, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(new McpReconnectResult
            {
                Name = name,
                Status = "connected",
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                Message = "reconnected"
            });

        public Task DisconnectAsync(WorkspacePaths paths, string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMcpToolRuntime(
        IReadOnlyDictionary<string, IReadOnlyList<McpResourceDefinition>> resourcesByServer) : IMcpToolRuntime
    {
        public Task<McpReconnectResult> ConnectServerAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpReconnectResult> ProbeServerAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DisconnectServerAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            Task.FromResult(resourcesByServer.TryGetValue(serverName, out var resources) ? resources : (IReadOnlyList<McpResourceDefinition>)[]);

        public Task<string> DescribeAsync(WorkspacePaths paths, JsonElement arguments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpToolDefinition> ResolveToolAsync(WorkspacePaths paths, string serverName, string toolName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpResourceReadResult> ReadResourceAsync(WorkspacePaths paths, string serverName, string uri, CancellationToken cancellationToken = default) =>
            Task.FromResult(new McpResourceReadResult
            {
                ServerName = serverName,
                Uri = uri,
                Output = $"resource {serverName}/{uri}"
            });

        public Task<McpPromptInvocationResult> GetPromptAsync(WorkspacePaths paths, string serverName, string promptName, JsonElement arguments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpToolInvocationResult> InvokeAsync(WorkspacePaths paths, string serverName, string toolName, JsonElement arguments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
