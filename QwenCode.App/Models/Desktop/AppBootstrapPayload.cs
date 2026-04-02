using QwenCode.App.Models;

namespace QwenCode.App.Models;

public sealed class AppBootstrapPayload
{
    public required string ProductName { get; init; }

    public required DesktopMode CurrentMode { get; init; }

    public required string CurrentLocale { get; init; }

    public required IReadOnlyList<LocaleOption> Locales { get; init; }

    public required string WorkspaceRoot { get; init; }

    public required IReadOnlyList<ResearchTrack> Tracks { get; init; }

    public required IReadOnlyList<string> CompatibilityGoals { get; init; }

    public required IReadOnlyList<CapabilityLane> CapabilityLanes { get; init; }

    public required IReadOnlyList<AdoptionPattern> AdoptionPatterns { get; init; }

    public required IReadOnlyList<SessionPreview> RecentSessions { get; init; }

    public required IReadOnlyList<ActiveTurnState> ActiveTurns { get; init; }

    public required IReadOnlyList<RecoverableTurnState> RecoverableTurns { get; init; }

    public required ProjectSummarySnapshot ProjectSummary { get; init; }

    public required QwenCompatibilitySnapshot QwenCompatibility { get; init; }

    public required QwenRuntimeProfile QwenRuntime { get; init; }

    public required ToolCatalogSnapshot QwenTools { get; init; }

    public required NativeToolHostSnapshot QwenNativeHost { get; init; }
}
