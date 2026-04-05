using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelPluginRuntimeService
{
    Task StartAsync(
        WorkspacePaths workspace,
        IReadOnlyList<ChannelDefinition> channels,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<ChannelDispatchResult> HandleInboundAsync(
        WorkspacePaths workspace,
        ChannelDefinition channel,
        ChannelRuntimeConfiguration configuration,
        JsonElement payload,
        CancellationToken cancellationToken = default);

    bool IsPluginChannel(WorkspacePaths workspace, string channelType);
}
