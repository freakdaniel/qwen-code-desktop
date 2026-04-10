namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Channel Block Streaming Chunk Configuration
/// </summary>
public sealed class ChannelBlockStreamingChunkConfiguration
{
    /// <summary>
    /// Gets or sets the min chars
    /// </summary>
    public int MinChars { get; init; } = 400;

    /// <summary>
    /// Gets or sets the max chars
    /// </summary>
    public int MaxChars { get; init; } = 1000;
}
