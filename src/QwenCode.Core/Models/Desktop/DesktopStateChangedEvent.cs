using QwenCode.App.Models;

namespace QwenCode.App.Models;

/// <summary>
/// Represents the Desktop State Changed Event
/// </summary>
public sealed class DesktopStateChangedEvent
{
    /// <summary>
    /// Gets or sets the current mode
    /// </summary>
    public required DesktopMode CurrentMode { get; init; }

    /// <summary>
    /// Gets or sets the current locale
    /// </summary>
    public required string CurrentLocale { get; init; }

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public required DateTime TimestampUtc { get; init; }
}
