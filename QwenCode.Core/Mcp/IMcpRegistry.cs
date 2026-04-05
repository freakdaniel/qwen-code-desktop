using QwenCode.App.Models;

namespace QwenCode.App.Mcp;

public interface IMcpRegistry
{
    IReadOnlyList<McpServerDefinition> ListServers(WorkspacePaths paths);

    McpServerDefinition AddServer(WorkspacePaths paths, McpServerRegistrationRequest request);

    bool RemoveServer(WorkspacePaths paths, string name, string scope);
}
