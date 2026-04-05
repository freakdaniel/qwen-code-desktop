namespace QwenCode.App.Models;

/// <summary>
/// Represents the Followup Suggestion
/// </summary>
public sealed class FollowupSuggestion
{
    /// <summary>
    /// Gets or sets the text
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets or sets the kind
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets or sets the source
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets or sets the confidence
    /// </summary>
    public int Confidence { get; init; }
}
