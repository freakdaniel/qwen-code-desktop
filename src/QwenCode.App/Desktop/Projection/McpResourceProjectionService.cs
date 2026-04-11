using Microsoft.Extensions.Options;
using QwenCode.App.Options;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Mcp;
using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the MCP Resource Projection Service.
/// </summary>
/// <param name="options">The options.</param>
/// <param name="workspacePathResolver">The workspace path resolver.</param>
/// <param name="resourceRegistryService">The resource registry service.</param>
public sealed class McpResourceProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IMcpResourceRegistryService resourceRegistryService) : IDesktopMcpResourceProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    /// <summary>
    /// Gets MCP resource registry async.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation.</param>
    /// <returns>A task that resolves to MCP resource registry snapshot.</returns>
    public Task<McpResourceRegistrySnapshot> GetResourceRegistryAsync(
        GetMcpResourceRegistryRequest request,
        CancellationToken cancellationToken = default) =>
        resourceRegistryService.GetSnapshotAsync(ResolveWorkspace(), request, cancellationToken);

    /// <summary>
    /// Reads registered MCP resource async.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation.</param>
    /// <returns>A task that resolves to MCP resource read result.</returns>
    public Task<McpResourceReadResult> ReadRegisteredResourceAsync(
        ReadMcpResourceRegistryEntryRequest request,
        CancellationToken cancellationToken = default) =>
        resourceRegistryService.ReadAsync(ResolveWorkspace(), request, cancellationToken);

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);
}
