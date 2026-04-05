using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelPluginRegistryService
{
    IReadOnlyList<ChannelPluginDefinition> ListPlugins(WorkspacePaths workspace);

    ChannelPluginDefinition? GetPlugin(WorkspacePaths workspace, string channelType);
}
