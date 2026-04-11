using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop MCP Resource Projection Service.
/// </summary>
public interface IDesktopMcpResourceProjectionService
{
    /// <summary>
    /// Gets MCP resource registry async.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation.</param>
    /// <returns>A task that resolves to MCP resource registry snapshot.</returns>
    Task<McpResourceRegistrySnapshot> GetResourceRegistryAsync(
        GetMcpResourceRegistryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads registered MCP resource async.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation.</param>
    /// <returns>A task that resolves to MCP resource read result.</returns>
    Task<McpResourceReadResult> ReadRegisteredResourceAsync(
        ReadMcpResourceRegistryEntryRequest request,
        CancellationToken cancellationToken = default);
}
