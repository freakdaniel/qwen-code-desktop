using QwenCode.Core.Models;

namespace QwenCode.Core.Channels;

/// <summary>
/// Defines the contract for Channel Adapter
/// </summary>
public interface IChannelAdapter
{
    /// <summary>
    /// Gets the channel type
    /// </summary>
    string ChannelType { get; }

    /// <summary>
    /// Connects async
    /// </summary>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ConnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects async
    /// </summary>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DisconnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Normalizes inbound
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="payload">The payload</param>
    /// <returns>The resulting channel envelope</returns>
    ChannelEnvelope NormalizeInbound(string channelName, JsonElement payload);

    /// <summary>
    /// Creates outbound payload
    /// </summary>
    /// <param name="route">The route</param>
    /// <param name="message">The message</param>
    /// <returns>The resulting json object</returns>
    JsonObject CreateOutboundPayload(ChannelSessionRoute route, ChannelOutboundMessage message);

    /// <summary>
    /// Executes send outbound async
    /// </summary>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="route">The route</param>
    /// <param name="message">The message</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to bool</returns>
    Task<bool> SendOutboundAsync(
        ChannelRuntimeConfiguration configuration,
        ChannelSessionRoute route,
        ChannelOutboundMessage message,
        JsonObject payload,
        CancellationToken cancellationToken = default);
}
