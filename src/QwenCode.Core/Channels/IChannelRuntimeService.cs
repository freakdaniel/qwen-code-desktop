using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

/// <summary>
/// Defines the contract for Channel Runtime Service
/// </summary>
public interface IChannelRuntimeService
{
    /// <summary>
    /// Gets snapshot
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <returns>The resulting channel snapshot</returns>
    ChannelSnapshot GetSnapshot(WorkspacePaths workspace);

    /// <summary>
    /// Starts async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to channel snapshot</returns>
    Task<ChannelSnapshot> StartAsync(WorkspacePaths workspace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes handle inbound async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channelName">The channel name</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to channel dispatch result</returns>
    Task<ChannelDispatchResult> HandleInboundAsync(
        WorkspacePaths workspace,
        string channelName,
        JsonElement payload,
        CancellationToken cancellationToken = default);
}
