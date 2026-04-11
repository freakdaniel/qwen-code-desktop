namespace QwenCode.Core.Mcp;

/// <summary>
/// Defines the contract for MCP resource registry services.
/// </summary>
public interface IMcpResourceRegistryService
{
    /// <summary>
    /// Gets snapshot async.
    /// </summary>
    /// <param name="paths">The paths to process.</param>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation.</param>
    /// <returns>A task that resolves to mcp resource registry snapshot.</returns>
    Task<McpResourceRegistrySnapshot> GetSnapshotAsync(
        WorkspacePaths paths,
        GetMcpResourceRegistryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads async.
    /// </summary>
    /// <param name="paths">The paths to process.</param>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation.</param>
    /// <returns>A task that resolves to mcp resource read result.</returns>
    Task<McpResourceReadResult> ReadAsync(
        WorkspacePaths paths,
        ReadMcpResourceRegistryEntryRequest request,
        CancellationToken cancellationToken = default);
}
