using QwenCode.App.Models;

namespace QwenCode.App.Mcp;

public interface IMcpConnectionManager
{
    IReadOnlyList<McpServerDefinition> ListServersWithStatus(WorkspacePaths paths);

    Task<McpReconnectResult> ReconnectAsync(
        WorkspacePaths paths,
        string name,
        CancellationToken cancellationToken = default);
}
