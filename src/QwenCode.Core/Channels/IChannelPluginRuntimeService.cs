using System.Text.Json;
using QwenCode.Core.Models;

namespace QwenCode.Core.Channels;

/// <summary>
/// Defines the contract for Channel Plugin Runtime Service
/// </summary>
public interface IChannelPluginRuntimeService
{
    /// <summary>
    /// Starts async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channels">The channels</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task StartAsync(
        WorkspacePaths workspace,
        IReadOnlyList<ChannelDefinition> channels,
        CancellationToken cancellationToken = default);

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
    /// <param name="channel">The channel</param>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to channel dispatch result</returns>
    Task<ChannelDispatchResult> HandleInboundAsync(
        WorkspacePaths workspace,
        ChannelDefinition channel,
        ChannelRuntimeConfiguration configuration,
        JsonElement payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes is plugin channel
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channelType">The channel type</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool IsPluginChannel(WorkspacePaths workspace, string channelType);
}
