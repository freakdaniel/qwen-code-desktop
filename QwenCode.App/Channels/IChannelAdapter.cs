using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelAdapter
{
    string ChannelType { get; }

    ChannelEnvelope NormalizeInbound(string channelName, JsonElement payload);
}
