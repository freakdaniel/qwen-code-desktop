using System.Text.Json.Nodes;
using QwenCode.Core.Models;

namespace QwenCode.Core.Ide;

/// <summary>
/// Defines the contract for Ide Companion Transport
/// </summary>
public interface IIdeCompanionTransport
{
    /// <summary>
    /// Connects async
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to bool</returns>
    Task<bool> ConnectAsync(IdeTransportConnectionInfo connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists tools async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to i read only list string</returns>
    Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes call tool async
    /// </summary>
    /// <param name="toolName">The tool name</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide tool call result</returns>
    Task<IdeToolCallResult> CallToolAsync(
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken = default);
}
