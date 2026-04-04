namespace QwenCode.App.Models;

public sealed class ChannelBlockStreamingChunkConfiguration
{
    public int MinChars { get; init; } = 400;

    public int MaxChars { get; init; } = 1000;
}
