namespace QwenCode.App.Models;

public sealed class GetFollowupSuggestionsRequest
{
    public string SessionId { get; init; } = string.Empty;

    public int MaxCount { get; init; } = 3;
}
