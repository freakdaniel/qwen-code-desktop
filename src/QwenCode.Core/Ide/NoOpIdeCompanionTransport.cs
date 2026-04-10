using System.Text.Json.Nodes;
using QwenCode.Core.Models;

namespace QwenCode.Core.Ide;

/// <summary>
/// Represents the No Op Ide Companion Transport
/// </summary>
public sealed class NoOpIdeCompanionTransport : IIdeCompanionTransport
{
    /// <summary>
    /// Connects async
    /// </summary>
    /// <param name="connection">The connection</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to bool</returns>
    public Task<bool> ConnectAsync(IdeTransportConnectionInfo connection, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <summary>
    /// Disconnects async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Lists tools async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to i read only list string</returns>
    public Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>
    /// Executes call tool async
    /// </summary>
    /// <param name="toolName">The tool name</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide tool call result</returns>
    public Task<IdeToolCallResult> CallToolAsync(
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new IdeToolCallResult
        {
            IsError = true,
            Text = "IDE companion transport is not connected."
        });
}
