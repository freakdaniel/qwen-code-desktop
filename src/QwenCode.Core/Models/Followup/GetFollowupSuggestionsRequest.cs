namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Get Followup Suggestions Request
/// </summary>
public sealed class GetFollowupSuggestionsRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the max count
    /// </summary>
    public int MaxCount { get; init; } = 3;
}
