namespace QwenCode.App.Models;

/// <summary>
/// Represents the Channel Block Streaming Coalesce Configuration
/// </summary>
public sealed class ChannelBlockStreamingCoalesceConfiguration
{
    /// <summary>
    /// Gets or sets the idle ms
    /// </summary>
    public int IdleMs { get; init; } = 1500;
}
