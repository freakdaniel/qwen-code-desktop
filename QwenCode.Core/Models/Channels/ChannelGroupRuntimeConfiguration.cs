namespace QwenCode.App.Models;

public sealed class ChannelGroupRuntimeConfiguration
{
    public required string ChatId { get; init; }

    public bool RequireMention { get; init; } = true;

    public string DispatchMode { get; init; } = string.Empty;
}
