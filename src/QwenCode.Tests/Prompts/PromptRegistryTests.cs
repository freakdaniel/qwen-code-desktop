using QwenCode.Core.Mcp;
using QwenCode.Core.Prompts;

namespace QwenCode.Tests.Prompts;

public sealed class PromptRegistryTests
{
    [Fact]
    public async Task PromptRegistryService_GetSnapshotAsync_DeduplicatesPromptNamesAcrossServers()
    {
        var paths = new WorkspacePaths
        {
            WorkspaceRoot = Path.Combine(Path.GetTempPath(), $"qwen-prompt-registry-{Guid.NewGuid():N}")
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
            new Dictionary<string, IReadOnlyList<McpPromptDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                ["docs-a"] =
                [
                    new McpPromptDefinition
                    {
                        ServerName = "docs-a",
                        Name = "workspace-summary",
                        Description = "Summarizes workspace A"
                    }
                ],
                ["docs-b"] =
                [
                    new McpPromptDefinition
                    {
                        ServerName = "docs-b",
                        Name = "workspace-summary",
                        Description = "Summarizes workspace B"
                    }
                ]
            });

        var service = new PromptRegistryService(connectionManager, runtime);

        var snapshot = await service.GetSnapshotAsync(paths, new GetPromptRegistryRequest());

        Assert.Equal(2, snapshot.TotalCount);
        Assert.Equal(2, snapshot.ServerCount);
        Assert.Contains(snapshot.Prompts, item => item.Name == "docs-a_workspace-summary");
        Assert.Contains(snapshot.Prompts, item => item.Name == "docs-b_workspace-summary");
        Assert.All(snapshot.Prompts, item => Assert.Equal(item.QualifiedName, $"{item.ServerName}/{item.PromptName}"));
    }

    [Fact]
    public async Task PromptRegistryService_InvokeAsync_ResolvesRenamedPromptAndCallsRuntime()
    {
        var paths = new WorkspacePaths
        {
            WorkspaceRoot = Path.Combine(Path.GetTempPath(), $"qwen-prompt-invoke-{Guid.NewGuid():N}")
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
            new Dictionary<string, IReadOnlyList<McpPromptDefinition>>(StringComparer.OrdinalIgnoreCase)
            {
                ["docs-a"] =
                [
                    new McpPromptDefinition
                    {
                        ServerName = "docs-a",
                        Name = "workspace-summary"
                    }
                ],
                ["docs-b"] =
                [
                    new McpPromptDefinition
                    {
                        ServerName = "docs-b",
                        Name = "workspace-summary"
                    }
                ]
            },
            (serverName, promptName, argumentsJson) => new McpPromptInvocationResult
            {
                ServerName = serverName,
                PromptName = promptName,
                Output = $"prompt {serverName}/{promptName}: {argumentsJson}"
            });

        var service = new PromptRegistryService(connectionManager, runtime);
        var snapshot = await service.GetSnapshotAsync(paths, new GetPromptRegistryRequest());
        var renamed = snapshot.Prompts.Single(item => string.Equals(item.Name, "docs-b_workspace-summary", StringComparison.Ordinal));

        var result = await service.InvokeAsync(paths, new InvokePromptRegistryEntryRequest
        {
            Name = renamed.Name,
            ArgumentsJson = """{"scope":"repo"}"""
        });

        Assert.Equal(renamed.ServerName, result.ServerName);
        Assert.Equal(renamed.PromptName, result.PromptName);
        Assert.Contains("\"scope\":\"repo\"", result.Output);
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
        IReadOnlyDictionary<string, IReadOnlyList<McpPromptDefinition>> promptsByServer,
        Func<string, string, string, McpPromptInvocationResult>? promptInvoker = null) : IMcpToolRuntime
    {
        public Task<McpReconnectResult> ConnectServerAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpReconnectResult> ProbeServerAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DisconnectServerAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(WorkspacePaths paths, string serverName, CancellationToken cancellationToken = default) =>
            Task.FromResult(promptsByServer.TryGetValue(serverName, out var prompts) ? prompts : (IReadOnlyList<McpPromptDefinition>)[]);

        public Task<string> DescribeAsync(WorkspacePaths paths, JsonElement arguments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpToolDefinition> ResolveToolAsync(WorkspacePaths paths, string serverName, string toolName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpResourceReadResult> ReadResourceAsync(WorkspacePaths paths, string serverName, string uri, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpPromptInvocationResult> GetPromptAsync(WorkspacePaths paths, string serverName, string promptName, JsonElement arguments, CancellationToken cancellationToken = default)
        {
            var argumentsJson = arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? "{}"
                : arguments.GetRawText();
            return Task.FromResult(promptInvoker?.Invoke(serverName, promptName, argumentsJson)
                ?? new McpPromptInvocationResult
                {
                    ServerName = serverName,
                    PromptName = promptName,
                    Output = argumentsJson
                });
        }

        public Task<McpToolInvocationResult> InvokeAsync(WorkspacePaths paths, string serverName, string toolName, JsonElement arguments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
