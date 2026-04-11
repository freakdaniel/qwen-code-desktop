namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Followup Suggestion Snapshot
/// </summary>
public sealed class FollowupSuggestionSnapshot
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the suppressed reason
    /// </summary>
    public string SuppressedReason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the generated at utc
    /// </summary>
    public string GeneratedAtUtc { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this snapshot was generated speculatively
    /// </summary>
    public bool IsSpeculative { get; init; }

    /// <summary>
    /// Gets or sets the cache status
    /// </summary>
    public string CacheStatus { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the session fingerprint used for cache validation
    /// </summary>
    public string Fingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the suggestions
    /// </summary>
    public IReadOnlyList<FollowupSuggestion> Suggestions { get; init; } = [];
}
