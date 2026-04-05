namespace QwenCode.App.Models;

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
    /// Gets or sets the suggestions
    /// </summary>
    public IReadOnlyList<FollowupSuggestion> Suggestions { get; init; } = [];
}
