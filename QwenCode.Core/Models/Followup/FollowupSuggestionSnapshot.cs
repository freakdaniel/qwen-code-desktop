namespace QwenCode.App.Models;

public sealed class FollowupSuggestionSnapshot
{
    public string SessionId { get; init; } = string.Empty;

    public string SuppressedReason { get; init; } = string.Empty;

    public IReadOnlyList<FollowupSuggestion> Suggestions { get; init; } = [];
}
