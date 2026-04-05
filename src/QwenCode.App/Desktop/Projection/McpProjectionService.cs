using Microsoft.Extensions.Options;
using QwenCode.App.Infrastructure;
using QwenCode.App.Mcp;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Mcp Projection Service
/// </summary>
/// <param name="options">The options</param>
/// <param name="workspacePathResolver">The workspace path resolver</param>
/// <param name="registry">The registry</param>
/// <param name="connectionManager">The connection manager</param>
public sealed class McpProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IMcpRegistry registry,
    IMcpConnectionManager connectionManager) : IDesktopMcpProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    /// <summary>
    /// Creates snapshot
    /// </summary>
    /// <returns>The resulting mcp snapshot</returns>
    public McpSnapshot CreateSnapshot() =>
        BuildSnapshot(connectionManager.ListServersWithStatus(ResolveWorkspace()));

    /// <summary>
    /// Executes add server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    public Task<McpSnapshot> AddServerAsync(McpServerRegistrationRequest request)
    {
        registry.AddServer(ResolveWorkspace(), request);
        return Task.FromResult(CreateSnapshot());
    }

    /// <summary>
    /// Removes server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    public Task<McpSnapshot> RemoveServerAsync(RemoveMcpServerRequest request)
    {
        var workspace = ResolveWorkspace();
        return RemoveServerCoreAsync(workspace, request);
    }

    /// <summary>
    /// Executes reconnect server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    public async Task<McpSnapshot> ReconnectServerAsync(
        ReconnectMcpServerRequest request,
        CancellationToken cancellationToken = default)
    {
        await connectionManager.ReconnectAsync(ResolveWorkspace(), request.Name, cancellationToken);
        return CreateSnapshot();
    }

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);

    private async Task<McpSnapshot> RemoveServerCoreAsync(WorkspacePaths workspace, RemoveMcpServerRequest request)
    {
        await connectionManager.DisconnectAsync(workspace, request.Name);
        registry.RemoveServer(workspace, request.Name, request.Scope);
        return CreateSnapshot();
    }

    private static McpSnapshot BuildSnapshot(IReadOnlyList<McpServerDefinition> servers) =>
        new()
        {
            TotalCount = servers.Count,
            ConnectedCount = servers.Count(static item => string.Equals(item.Status, "connected", StringComparison.OrdinalIgnoreCase)),
            DisconnectedCount = servers.Count(static item => string.Equals(item.Status, "disconnected", StringComparison.OrdinalIgnoreCase)),
            MissingCount = servers.Count(static item => string.Equals(item.Status, "missing", StringComparison.OrdinalIgnoreCase)),
            TokenCount = servers.Count(static item => item.HasPersistedToken),
            Servers = servers
        };
}
