using QwenCode.Core.Models;

namespace QwenCode.Core.Mcp;

/// <summary>
/// Defines the contract for Mcp Registry
/// </summary>
public interface IMcpRegistry
{
    /// <summary>
    /// Lists servers
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting i read only list mcp server definition</returns>
    IReadOnlyList<McpServerDefinition> ListServers(WorkspacePaths paths);

    /// <summary>
    /// Executes add server
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting mcp server definition</returns>
    McpServerDefinition AddServer(WorkspacePaths paths, McpServerRegistrationRequest request);

    /// <summary>
    /// Removes server
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="name">The name</param>
    /// <param name="scope">The scope</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool RemoveServer(WorkspacePaths paths, string name, string scope);
}
