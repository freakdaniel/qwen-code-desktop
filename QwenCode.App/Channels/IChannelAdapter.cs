using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelAdapter
{
    string ChannelType { get; }

    Task ConnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default);

    Task DisconnectAsync(ChannelRuntimeConfiguration configuration, CancellationToken cancellationToken = default);

    ChannelEnvelope NormalizeInbound(string channelName, JsonElement payload);

    JsonObject CreateOutboundPayload(ChannelSessionRoute route, ChannelOutboundMessage message);

    Task<bool> SendOutboundAsync(
        ChannelRuntimeConfiguration configuration,
        ChannelSessionRoute route,
        ChannelOutboundMessage message,
        JsonObject payload,
        CancellationToken cancellationToken = default);
}
