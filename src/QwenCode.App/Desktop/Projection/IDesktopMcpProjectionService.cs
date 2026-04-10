using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Mcp Projection Service
/// </summary>
public interface IDesktopMcpProjectionService
{
    /// <summary>
    /// Creates snapshot
    /// </summary>
    /// <returns>The resulting mcp snapshot</returns>
    McpSnapshot CreateSnapshot();

    /// <summary>
    /// Executes add server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    Task<McpSnapshot> AddServerAsync(McpServerRegistrationRequest request);

    /// <summary>
    /// Removes server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    Task<McpSnapshot> RemoveServerAsync(RemoveMcpServerRequest request);

    /// <summary>
    /// Executes reconnect server async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp snapshot</returns>
    Task<McpSnapshot> ReconnectServerAsync(ReconnectMcpServerRequest request, CancellationToken cancellationToken = default);
}
