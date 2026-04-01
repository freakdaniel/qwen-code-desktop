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

    public required IReadOnlyList<CapabilityLane> CapabilityLanes { get; init; }

    public required IReadOnlyList<AdoptionPattern> AdoptionPatterns { get; init; }

    public required IReadOnlyList<SessionPreview> RecentSessions { get; init; }

    public required IReadOnlyList<SourceMirrorStatus> SourceStatuses { get; init; }

    public required IReadOnlyList<RuntimePortWorkItem> RuntimePortPlan { get; init; }

    public required QwenCompatibilitySnapshot QwenCompatibility { get; init; }

    public required QwenRuntimeProfile QwenRuntime { get; init; }

    public required QwenToolCatalogSnapshot QwenTools { get; init; }

    public required QwenNativeToolHostSnapshot QwenNativeHost { get; init; }
}
