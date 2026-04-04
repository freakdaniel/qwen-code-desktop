namespace QwenCode.App.Models;

public sealed class FollowupSuggestion
{
    public required string Text { get; init; }

    public required string Kind { get; init; }

    public required string Source { get; init; }

    public int Confidence { get; init; }
}
