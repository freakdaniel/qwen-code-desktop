using System.Collections.Concurrent;
namespace QwenCode.Core.Followup;

/// <summary>
/// Represents an in-memory follow-up suggestion cache
/// </summary>
public sealed class InMemoryFollowupSuggestionCache : IFollowupSuggestionCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryGet(string sessionId, string fingerprint, out FollowupSuggestionSnapshot snapshot)
    {
        snapshot = new FollowupSuggestionSnapshot();
        if (string.IsNullOrWhiteSpace(sessionId) ||
            string.IsNullOrWhiteSpace(fingerprint) ||
            !entries.TryGetValue(sessionId, out var entry) ||
            !string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        snapshot = entry.Snapshot;
        return true;
    }

    /// <inheritdoc />
    public void Set(string sessionId, string fingerprint, FollowupSuggestionSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(fingerprint))
        {
            return;
        }

        entries[sessionId] = new CacheEntry(fingerprint, snapshot);
    }

    private sealed record CacheEntry(string Fingerprint, FollowupSuggestionSnapshot Snapshot);
}
