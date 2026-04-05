using QwenCode.App.Models;

namespace QwenCode.App.Models;

/// <summary>
/// Represents the App Bootstrap Payload
/// </summary>
public sealed class AppBootstrapPayload
{
    /// <summary>
    /// Gets or sets the product name
    /// </summary>
    public required string ProductName { get; init; }

    /// <summary>
    /// Gets or sets the current mode
    /// </summary>
    public required DesktopMode CurrentMode { get; init; }

    /// <summary>
    /// Gets or sets the current locale
    /// </summary>
    public required string CurrentLocale { get; init; }

    /// <summary>
    /// Gets or sets the locales
    /// </summary>
    public required IReadOnlyList<LocaleOption> Locales { get; init; }

    /// <summary>
    /// Gets or sets the workspace root
    /// </summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// Gets or sets the tracks
    /// </summary>
    public required IReadOnlyList<ResearchTrack> Tracks { get; init; }

    /// <summary>
    /// Gets or sets the compatibility goals
    /// </summary>
    public required IReadOnlyList<string> CompatibilityGoals { get; init; }

    /// <summary>
    /// Gets or sets the capability lanes
    /// </summary>
    public required IReadOnlyList<CapabilityLane> CapabilityLanes { get; init; }

    /// <summary>
    /// Gets or sets the adoption patterns
    /// </summary>
    public required IReadOnlyList<AdoptionPattern> AdoptionPatterns { get; init; }

    /// <summary>
    /// Gets or sets the recent sessions
    /// </summary>
    public required IReadOnlyList<SessionPreview> RecentSessions { get; init; }

    /// <summary>
    /// Gets or sets the active turns
    /// </summary>
    public required IReadOnlyList<ActiveTurnState> ActiveTurns { get; init; }

    /// <summary>
    /// Gets or sets the active arena sessions
    /// </summary>
    public required IReadOnlyList<ActiveArenaSessionState> ActiveArenaSessions { get; init; }

    /// <summary>
    /// Gets or sets the recoverable turns
    /// </summary>
    public required IReadOnlyList<RecoverableTurnState> RecoverableTurns { get; init; }

    /// <summary>
    /// Gets or sets the project summary
    /// </summary>
    public required ProjectSummarySnapshot ProjectSummary { get; init; }

    /// <summary>
    /// Gets or sets the qwen compatibility
    /// </summary>
    public required QwenCompatibilitySnapshot QwenCompatibility { get; init; }

    /// <summary>
    /// Gets or sets the qwen runtime
    /// </summary>
    public required QwenRuntimeProfile QwenRuntime { get; init; }

    /// <summary>
    /// Gets or sets the qwen tools
    /// </summary>
    public required ToolCatalogSnapshot QwenTools { get; init; }

    /// <summary>
    /// Gets or sets the qwen native host
    /// </summary>
    public required NativeToolHostSnapshot QwenNativeHost { get; init; }

    /// <summary>
    /// Gets or sets the qwen auth
    /// </summary>
    public required AuthStatusSnapshot QwenAuth { get; init; }

    /// <summary>
    /// Gets or sets the qwen mcp
    /// </summary>
    public required McpSnapshot QwenMcp { get; init; }

    /// <summary>
    /// Gets or sets the qwen extensions
    /// </summary>
    public required ExtensionSnapshot QwenExtensions { get; init; }

    /// <summary>
    /// Gets or sets the qwen channels
    /// </summary>
    public required ChannelSnapshot QwenChannels { get; init; }

    /// <summary>
    /// Gets or sets the qwen workspace
    /// </summary>
    public required WorkspaceSnapshot QwenWorkspace { get; init; }
}
