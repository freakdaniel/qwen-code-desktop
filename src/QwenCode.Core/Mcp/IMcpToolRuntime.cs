using QwenCode.Core.Models;

namespace QwenCode.Core.Mcp;

/// <summary>
/// Defines the contract for Mcp Tool Runtime
/// </summary>
public interface IMcpToolRuntime
{
    /// <summary>
    /// Connects server async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp reconnect result</returns>
    Task<McpReconnectResult> ConnectServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes probe server async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp reconnect result</returns>
    Task<McpReconnectResult> ProbeServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects server async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DisconnectServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists prompts async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to i read only list mcp prompt definition</returns>
    Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists resources async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to i read only list mcp resource definition</returns>
    Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes describe async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
    Task<string> DescribeAsync(
        WorkspacePaths paths,
        JsonElement arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves tool async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp tool definition</returns>
    Task<McpToolDefinition> ResolveToolAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads resource async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="uri">The uri</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp resource read result</returns>
    Task<McpResourceReadResult> ReadResourceAsync(
        WorkspacePaths paths,
        string serverName,
        string uri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets prompt async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="promptName">The prompt name</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp prompt invocation result</returns>
    Task<McpPromptInvocationResult> GetPromptAsync(
        WorkspacePaths paths,
        string serverName,
        string promptName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp tool invocation result</returns>
    Task<McpToolInvocationResult> InvokeAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
