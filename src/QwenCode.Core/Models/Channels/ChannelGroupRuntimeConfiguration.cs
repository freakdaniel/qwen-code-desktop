namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Channel Group Runtime Configuration
/// </summary>
public sealed class ChannelGroupRuntimeConfiguration
{
    /// <summary>
    /// Gets or sets the chat id
    /// </summary>
    public required string ChatId { get; init; }

    /// <summary>
    /// Gets or sets the require mention
    /// </summary>
    public bool RequireMention { get; init; } = true;

    /// <summary>
    /// Gets or sets the dispatch mode
    /// </summary>
    public string DispatchMode { get; init; } = string.Empty;
}
