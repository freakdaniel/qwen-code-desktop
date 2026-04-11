using QwenCode.Core.Extensions;
using QwenCode.Core.Models;

namespace QwenCode.Core.Channels;

/// <summary>
/// Represents the Channel Plugin Registry Service
/// </summary>
/// <param name="extensionCatalogService">The extension catalog service</param>
public sealed class ChannelPluginRegistryService(
    IExtensionCatalogService extensionCatalogService) : IChannelPluginRegistryService
{
    /// <summary>
    /// Lists plugins
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <returns>The resulting i read only list channel plugin definition</returns>
    public IReadOnlyList<ChannelPluginDefinition> ListPlugins(WorkspacePaths workspace)
    {
        var snapshot = extensionCatalogService.Inspect(workspace);
        var plugins = new List<ChannelPluginDefinition>();

        foreach (var extension in snapshot.Extensions.Where(static item => item.IsActive))
        {
            var manifestPath = Path.Combine(extension.Path, "qwen-extension.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            JsonObject? manifestRoot;
            try
            {
                manifestRoot = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject;
            }
            catch
            {
                continue;
            }

            if (manifestRoot?["channels"] is not JsonObject channelsObject)
            {
                continue;
            }

            foreach (var channelEntry in channelsObject)
            {
                if (channelEntry.Value is not JsonObject channelObject)
                {
                    continue;
                }

                var entry = channelObject["entry"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(channelEntry.Key) || string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                var entryPath = Path.IsPathRooted(entry)
                    ? Path.GetFullPath(entry)
                    : Path.GetFullPath(Path.Combine(extension.Path, entry));
                if (!File.Exists(entryPath))
                {
                    continue;
                }

                plugins.Add(new ChannelPluginDefinition
                {
                    ExtensionName = extension.Name,
                    ChannelType = channelEntry.Key,
                    DisplayName = channelObject["displayName"]?.GetValue<string>() ?? channelEntry.Key,
                    EntryPath = entryPath,
                    ExtensionPath = extension.Path
                });
            }
        }

        return plugins
            .OrderBy(static item => item.ChannelType, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gets plugin
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channelType">The channel type</param>
    /// <returns>The resulting channel plugin definition?</returns>
    public ChannelPluginDefinition? GetPlugin(WorkspacePaths workspace, string channelType) =>
        ListPlugins(workspace).FirstOrDefault(item =>
            string.Equals(item.ChannelType, channelType, StringComparison.OrdinalIgnoreCase));
}
