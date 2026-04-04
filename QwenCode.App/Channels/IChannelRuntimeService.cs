using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelRuntimeService
{
    ChannelSnapshot GetSnapshot(WorkspacePaths workspace);

    Task<ChannelSnapshot> StartAsync(WorkspacePaths workspace, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<ChannelDispatchResult> HandleInboundAsync(
        WorkspacePaths workspace,
        string channelName,
        JsonElement payload,
        CancellationToken cancellationToken = default);
}
