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

            var scriptPath = CrossPlatformTestSupport.CreateExecutableScript(
                toolRoot,
                "fake-mcp",
                """
                while (($line = [Console]::In.ReadLine()) -ne $null) {
                    if ([string]::IsNullOrWhiteSpace($line)) {
                        continue
                    }

                    $request = $line | ConvertFrom-Json
                    if (-not $request.id) {
                        continue
                    }

                    $result = switch ($request.method) {
                        "initialize" {
                            @{
                                protocolVersion = "2025-06-18"
                                capabilities = @{ tools = @{} }
                                serverInfo = @{
                                    name = "fake-stdio-mcp"
                                    version = "1.0.0"
                                }
                            }
                            break
                        }
                        "tools/list" {
                            @{
                                tools = @(
                                    @{
                                        name = "demo"
                                        description = "Demo tool"
                                        inputSchema = @{
                                            type = "object"
                                        }
                                    }
                                )
                            }
                            break
                        }
                        default {
                            @{}
                            break
                        }
                    }

                    $response = @{
                        jsonrpc = "2.0"
                        id = $request.id
                        result = $result
                    } | ConvertTo-Json -Depth 20 -Compress

                    [Console]::Out.WriteLine($response)
                }
                """,
                """
                while IFS= read -r line; do
                  [ -z "$line" ] && continue
                  id="$(printf '%s' "$line" | sed -n 's/.*"id":\("[^"]*"\|[0-9][0-9]*\).*/\1/p')"
                  [ -z "$id" ] && continue

                  case "$line" in
                    *'"method":"initialize"'*)
                      result='{"protocolVersion":"2025-06-18","capabilities":{"tools":{}},"serverInfo":{"name":"fake-stdio-mcp","version":"1.0.0"}}'
                      ;;
                    *'"method":"tools/list"'*)
                      result='{"tools":[{"name":"demo","description":"Demo tool","inputSchema":{"type":"object"}}]}'
                      ;;
                    *)
                      result='{}'
                      ;;
                  esac

                  printf '{"jsonrpc":"2.0","id":%s,"result":%s}\n' "$id" "$result"
                done
                """);

            var paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var environment = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environment);
            var registry = new McpRegistryService(
                runtimeProfileService,
                new FileMcpTokenStore(environment));
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "local-demo",
                Scope = "user",
                Transport = "stdio",
                CommandOrUrl = OperatingSystem.IsWindows()
                    ? Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")
                    : scriptPath,
                Arguments = OperatingSystem.IsWindows()
                    ? ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath]
                    : []
            });

            var manager = new McpConnectionManagerService(
                registry,
                new McpToolRuntimeService(registry, new FileMcpTokenStore(environment), new HttpClient(), runtimeProfileService));
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
            var runtimeProfileService = new QwenRuntimeProfileService(environment);
            var registry = new McpRegistryService(
                runtimeProfileService,
                new FileMcpTokenStore(environment));
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "broken-http",
                Scope = "user",
                Transport = "http",
                CommandOrUrl = "https://127.0.0.1:1/mcp"
            });

            var manager = new McpConnectionManagerService(
                registry,
                new McpToolRuntimeService(registry, new FileMcpTokenStore(environment), new HttpClient(), runtimeProfileService));
            var result = await manager.ReconnectAsync(paths, "broken-http");

            Assert.Equal("disconnected", result.Status);
            Assert.NotEmpty(result.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task McpConnectionManagerService_ReconnectAsync_PersistsDiscoveredToolMetadata()
    {
        var paths = new WorkspacePaths { WorkspaceRoot = Path.GetTempPath() };
        var registry = new FakeMcpRegistry(
            new McpServerDefinition
            {
                Name = "docs",
                Scope = "user",
                Transport = "http",
                Instructions = "Use the docs server before generic web search."
            });
        var runtime = new FakeMcpToolRuntime(
            new McpReconnectResult
            {
                Name = "docs",
                Status = "connected",
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                Message = "connected",
                DiscoveredToolsCount = 3,
                DiscoveredPromptsCount = 2,
                SupportsPrompts = true,
                SupportsResources = true,
                LastDiscoveryUtc = DateTimeOffset.UtcNow.AddSeconds(-10)
            });
        var manager = new McpConnectionManagerService(registry, runtime);

        var result = await manager.ReconnectAsync(paths, "docs");
        var listed = Assert.Single(manager.ListServersWithStatus(paths));

        Assert.Equal(3, result.DiscoveredToolsCount);
        Assert.Equal(3, listed.DiscoveredToolsCount);
        Assert.Equal(2, listed.DiscoveredPromptsCount);
        Assert.Equal("Use the docs server before generic web search.", listed.Instructions);
        Assert.True(listed.SupportsPrompts);
        Assert.True(listed.SupportsResources);
        Assert.NotNull(listed.LastDiscoveryUtc);
    }

    [Fact]
    public async Task McpConnectionManagerService_HealthMonitor_ReconnectsAfterProbeFailures()
    {
        var paths = new WorkspacePaths { WorkspaceRoot = Path.GetTempPath() };
        var registry = new FakeMcpRegistry(
            new McpServerDefinition
            {
                Name = "docs",
                Scope = "user",
                Transport = "http"
            });
        var runtime = new ControllableMcpToolRuntime(
            connectResults:
            [
                new McpReconnectResult
                {
                    Name = "docs",
                    Status = "connected",
                    AttemptedAtUtc = DateTimeOffset.UtcNow,
                    Message = "initial connect",
                    DiscoveredToolsCount = 2
                },
                new McpReconnectResult
                {
                    Name = "docs",
                    Status = "connected",
                    AttemptedAtUtc = DateTimeOffset.UtcNow,
                    Message = "reconnected",
                    DiscoveredToolsCount = 2
                }
            ],
            probeSequence:
            [
                new InvalidOperationException("probe failure 1"),
                new InvalidOperationException("probe failure 2")
            ]);
        var manager = new McpConnectionManagerService(
            registry,
            runtime,
            new McpHealthMonitorOptions
            {
                CheckInterval = TimeSpan.FromMilliseconds(25),
                MaxConsecutiveFailures = 2,
                ReconnectDelay = TimeSpan.FromMilliseconds(10),
                AutoReconnect = true
            });

        var initial = await manager.ReconnectAsync(paths, "docs");
        Assert.Equal("connected", initial.Status);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        McpServerDefinition? listed = null;
        while (DateTime.UtcNow < deadline)
        {
            listed = manager.ListServersWithStatus(paths).Single();
            if (string.Equals(listed.Status, "connected", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(listed.LastError, string.Empty, StringComparison.Ordinal) &&
                runtime.ConnectCalls >= 2)
            {
                break;
            }

            await Task.Delay(50);
        }

        listed = manager.ListServersWithStatus(paths).Single();
        Assert.True(runtime.ConnectCalls >= 2);
        Assert.True(runtime.DisconnectCalls >= 1);
        Assert.Equal("connected", listed.Status);
        Assert.Equal(2, listed.DiscoveredToolsCount);
    }

    private sealed class FakeMcpRegistry(McpServerDefinition server) : IMcpRegistry
    {
        public IReadOnlyList<McpServerDefinition> ListServers(WorkspacePaths paths) => [server];

        public McpServerDefinition AddServer(WorkspacePaths paths, McpServerRegistrationRequest request) =>
            throw new NotSupportedException();

        public bool RemoveServer(WorkspacePaths paths, string name, string scope) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMcpToolRuntime(McpReconnectResult reconnectResult) : IMcpToolRuntime
    {
        public Task<McpReconnectResult> ConnectServerAsync(
            WorkspacePaths paths,
            string serverName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(reconnectResult);

        public Task<McpReconnectResult> ProbeServerAsync(
            WorkspacePaths paths,
            string serverName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(reconnectResult);

        public Task DisconnectServerAsync(
            WorkspacePaths paths,
            string serverName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> DescribeAsync(
            WorkspacePaths paths,
            JsonElement arguments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(
            WorkspacePaths paths,
            string serverName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpToolDefinition> ResolveToolAsync(
            WorkspacePaths paths,
            string serverName,
            string toolName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpResourceReadResult> ReadResourceAsync(
            WorkspacePaths paths,
            string serverName,
            string uri,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpPromptInvocationResult> GetPromptAsync(
            WorkspacePaths paths,
            string serverName,
            string promptName,
            JsonElement arguments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpToolInvocationResult> InvokeAsync(
            WorkspacePaths paths,
            string serverName,
            string toolName,
            JsonElement arguments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ControllableMcpToolRuntime(
        IReadOnlyList<McpReconnectResult> connectResults,
        IReadOnlyList<Exception> probeSequence) : IMcpToolRuntime
    {
        private readonly Queue<McpReconnectResult> connectQueue = new(connectResults);
        private readonly Queue<Exception> probeQueue = new(probeSequence);

        public int ConnectCalls { get; private set; }

        public int DisconnectCalls { get; private set; }

        public Task<McpReconnectResult> ConnectServerAsync(
            WorkspacePaths paths,
            string serverName,
            CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            var result = connectQueue.Count > 0
                ? connectQueue.Dequeue()
                : new McpReconnectResult
                {
                    Name = serverName,
                    Status = "connected",
                    AttemptedAtUtc = DateTimeOffset.UtcNow,
                    Message = "connected",
                    DiscoveredToolsCount = 0
                };
            return Task.FromResult(result);
        }

        public Task<McpReconnectResult> ProbeServerAsync(
            WorkspacePaths paths,
            string serverName,
            CancellationToken cancellationToken = default)
        {
            if (probeQueue.Count > 0)
            {
                throw probeQueue.Dequeue();
            }

            return Task.FromResult(new McpReconnectResult
            {
                Name = serverName,
                Status = "connected",
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                Message = "healthy",
                DiscoveredToolsCount = 2,
                LastDiscoveryUtc = DateTimeOffset.UtcNow
            });
        }

        public Task DisconnectServerAsync(
            WorkspacePaths paths,
            string serverName,
            CancellationToken cancellationToken = default)
        {
            DisconnectCalls++;
            return Task.CompletedTask;
        }

        public Task<string> DescribeAsync(
            WorkspacePaths paths,
            JsonElement arguments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(
            WorkspacePaths paths,
            string serverName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpToolDefinition> ResolveToolAsync(
            WorkspacePaths paths,
            string serverName,
            string toolName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpResourceReadResult> ReadResourceAsync(
            WorkspacePaths paths,
            string serverName,
            string uri,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpPromptInvocationResult> GetPromptAsync(
            WorkspacePaths paths,
            string serverName,
            string promptName,
            JsonElement arguments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<McpToolInvocationResult> InvokeAsync(
            WorkspacePaths paths,
            string serverName,
            string toolName,
            JsonElement arguments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
