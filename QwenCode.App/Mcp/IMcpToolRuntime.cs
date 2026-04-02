using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Mcp;

public interface IMcpToolRuntime
{
    Task<string> DescribeAsync(
        WorkspacePaths paths,
        JsonElement arguments,
        CancellationToken cancellationToken = default);

    Task<McpToolDefinition> ResolveToolAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        CancellationToken cancellationToken = default);

    Task<McpToolInvocationResult> InvokeAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
