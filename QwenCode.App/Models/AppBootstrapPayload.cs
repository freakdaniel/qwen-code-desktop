using QwenCode.App.Enums;

namespace QwenCode.App.Models;

public sealed class AppBootstrapPayload
{
    public required string ProductName { get; init; }

    public required DesktopMode CurrentMode { get; init; }

    public required string CurrentLocale { get; init; }

    public required IReadOnlyList<LocaleOption> Locales { get; init; }

    public required SourceMirrorPaths Sources { get; init; }

    public required IReadOnlyList<ResearchTrack> Tracks { get; init; }

    public required IReadOnlyList<string> CompatibilityGoals { get; init; }
}
