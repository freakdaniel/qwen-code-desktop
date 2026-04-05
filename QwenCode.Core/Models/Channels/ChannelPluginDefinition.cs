namespace QwenCode.App.Models;

public sealed class ChannelPluginDefinition
{
    public required string ExtensionName { get; init; }

    public required string ChannelType { get; init; }

    public required string DisplayName { get; init; }

    public required string EntryPath { get; init; }

    public required string ExtensionPath { get; init; }
}
