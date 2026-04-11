namespace QwenCode.Core.Followup;

/// <summary>
/// Defines the contract for follow-up suggestion cache
/// </summary>
public interface IFollowupSuggestionCache
{
    /// <summary>
    /// Tries to get a cached snapshot
    /// </summary>
    /// <param name="sessionId">The session id</param>
    /// <param name="fingerprint">The fingerprint</param>
    /// <param name="snapshot">The cached snapshot</param>
    /// <returns>True when a valid snapshot exists</returns>
    bool TryGet(string sessionId, string fingerprint, out FollowupSuggestionSnapshot snapshot);

    /// <summary>
    /// Stores a snapshot
    /// </summary>
    /// <param name="sessionId">The session id</param>
    /// <param name="fingerprint">The fingerprint</param>
    /// <param name="snapshot">The snapshot</param>
    void Set(string sessionId, string fingerprint, FollowupSuggestionSnapshot snapshot);
}
