using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Mcp;

public interface IMcpToolRuntime
{
    Task<McpReconnectResult> ConnectServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    Task<McpReconnectResult> ProbeServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    Task DisconnectServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    Task<string> DescribeAsync(
        WorkspacePaths paths,
        JsonElement arguments,
        CancellationToken cancellationToken = default);

    Task<McpToolDefinition> ResolveToolAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        CancellationToken cancellationToken = default);

    Task<McpResourceReadResult> ReadResourceAsync(
        WorkspacePaths paths,
        string serverName,
        string uri,
        CancellationToken cancellationToken = default);

    Task<McpPromptInvocationResult> GetPromptAsync(
        WorkspacePaths paths,
        string serverName,
        string promptName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);

    Task<McpToolInvocationResult> InvokeAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
