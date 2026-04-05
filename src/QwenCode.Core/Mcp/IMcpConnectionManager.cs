using QwenCode.App.Models;

namespace QwenCode.App.Mcp;

/// <summary>
/// Defines the contract for Mcp Connection Manager
/// </summary>
public interface IMcpConnectionManager
{
    /// <summary>
    /// Lists servers with status
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting i read only list mcp server definition</returns>
    IReadOnlyList<McpServerDefinition> ListServersWithStatus(WorkspacePaths paths);

    /// <summary>
    /// Executes reconnect async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="name">The name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp reconnect result</returns>
    Task<McpReconnectResult> ReconnectAsync(
        WorkspacePaths paths,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="name">The name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DisconnectAsync(
        WorkspacePaths paths,
        string name,
        CancellationToken cancellationToken = default);
}
