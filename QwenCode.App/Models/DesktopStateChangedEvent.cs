using QwenCode.App.Enums;

namespace QwenCode.App.Models;

public sealed class DesktopStateChangedEvent
{
    public required DesktopMode CurrentMode { get; init; }

    public required string CurrentLocale { get; init; }

    public required DateTime TimestampUtc { get; init; }
}
