using QwenCode.App.Models;

namespace QwenCode.App.Channels;

/// <summary>
/// Defines the contract for Channel Plugin Registry Service
/// </summary>
public interface IChannelPluginRegistryService
{
    /// <summary>
    /// Lists plugins
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <returns>The resulting i read only list channel plugin definition</returns>
    IReadOnlyList<ChannelPluginDefinition> ListPlugins(WorkspacePaths workspace);

    /// <summary>
    /// Gets plugin
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channelType">The channel type</param>
    /// <returns>The resulting channel plugin definition?</returns>
    ChannelPluginDefinition? GetPlugin(WorkspacePaths workspace, string channelType);
}
