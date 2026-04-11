using QwenCode.Core.Agents;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Models;
using QwenCode.Core.Sessions;

namespace QwenCode.Core.Followup;

/// <summary>
/// Represents the Followup Suggestion Service
/// </summary>
/// <param name="transcriptStore">The transcript store</param>
/// <param name="activeTurnRegistry">The active turn registry</param>
/// <param name="interruptedTurnStore">The interrupted turn store</param>
/// <param name="arenaSessionRegistry">The arena session registry</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="providerBackedGenerator">The provider backed generator</param>
/// <param name="suggestionCache">The suggestion cache</param>
public sealed class FollowupSuggestionService(
    ITranscriptStore transcriptStore,
    IActiveTurnRegistry activeTurnRegistry,
    IInterruptedTurnStore interruptedTurnStore,
    IArenaSessionRegistry arenaSessionRegistry,
    QwenRuntimeProfileService runtimeProfileService,
    IFollowupSuggestionGenerator? providerBackedGenerator = null,
    IFollowupSuggestionCache? suggestionCache = null) : IFollowupSuggestionService
{
    /// <summary>
    /// Gets suggestions async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to followup suggestion snapshot</returns>
    public async Task<FollowupSuggestionSnapshot> GetSuggestionsAsync(
        WorkspacePaths paths,
        GetFollowupSuggestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessionId = ResolveSessionId(paths, request.SessionId);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return new FollowupSuggestionSnapshot
            {
                SuppressedReason = "missing_session"
            };
        }

        var detail = transcriptStore.GetSession(paths, new GetDesktopSessionRequest
        {
            SessionId = sessionId
        });
        if (detail is null)
        {
            return new FollowupSuggestionSnapshot
            {
                SessionId = sessionId,
                SuppressedReason = "missing_session"
            };
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var session = detail.Session;
        var hasActiveTurn = activeTurnRegistry.ListActiveTurns()
            .Any(item => string.Equals(item.SessionId, sessionId, StringComparison.Ordinal));
        var hasRecoverableTurn = interruptedTurnStore.ListRecoverableTurns(runtimeProfile.ChatsDirectory)
            .Any(item => string.Equals(item.SessionId, sessionId, StringComparison.Ordinal));
        var hasActiveArenaSession = arenaSessionRegistry.ListActiveSessions()
            .Any(item => string.Equals(item.WorkingDirectory, session.WorkingDirectory, StringComparison.OrdinalIgnoreCase));
        var maxCount = request.MaxCount <= 0 ? 3 : request.MaxCount;
        var fingerprint = BuildFingerprint(detail, hasActiveTurn, hasRecoverableTurn, hasActiveArenaSession);

        if (!hasActiveTurn &&
            suggestionCache is not null &&
            suggestionCache.TryGet(sessionId, fingerprint, out var cachedSnapshot))
        {
            return SliceSnapshot(cachedSnapshot, maxCount, "hit");
        }

        var suggestions = new List<FollowupSuggestion>();

        if (providerBackedGenerator is not null)
        {
            var providerSuggestion = await providerBackedGenerator.GenerateAsync(paths, detail, cancellationToken);
            if (providerSuggestion is not null)
            {
                suggestions.Add(providerSuggestion);
            }
        }

        if (hasActiveTurn)
        {
            AddSuggestion(suggestions, "wait for the current turn to finish", "active-turn", "runtime", 95);
        }

        if (hasRecoverableTurn)
        {
            AddSuggestion(suggestions, "resume the interrupted turn", "resume", "runtime", 100);
        }

        if (detail.Summary.PendingQuestionCount > 0)
        {
            AddSuggestion(suggestions, "answer the pending question", "pending-question", "session", 100);
        }

        if (detail.Summary.PendingApprovalCount > 0)
        {
            AddSuggestion(suggestions, "approve the pending tool", "pending-approval", "session", 100);
        }

        if (hasActiveArenaSession)
        {
            AddSuggestion(suggestions, "check arena status", "arena", "arena", 80);
        }

        var latestTool = detail.Entries.LastOrDefault(static entry => entry.Type == "tool");
        var latestCommand = detail.Entries.LastOrDefault(static entry => entry.Type == "command");
        if (latestTool is not null && string.Equals(latestTool.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            AddSuggestion(suggestions, "fix the failing tool call", "tool-failure", "history", 85);
        }

        if (latestCommand is not null && string.Equals(latestCommand.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            AddSuggestion(suggestions, "fix the failing command", "command-failure", "history", 85);
        }

        var changedFiles = detail.Entries
            .Where(static entry => entry.Type == "tool")
            .SelectMany(static entry => entry.ChangedFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (changedFiles.Length > 0)
        {
            AddSuggestion(suggestions, "review the changes", "review", "history", 70);

            var hasRecentTestExecution = detail.Entries.Any(entry =>
                (entry.Type == "command" || entry.Type == "tool") &&
                (entry.Body.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                 entry.Arguments.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                 entry.Title.Contains("test", StringComparison.OrdinalIgnoreCase)));
            if (!hasRecentTestExecution)
            {
                AddSuggestion(suggestions, "run the tests", "verify", "history", 75);
            }
        }

        if (latestTool is not null && string.Equals(latestTool.ToolName, "arena", StringComparison.OrdinalIgnoreCase))
        {
            AddSuggestion(suggestions, "select a winner", "arena-followup", "arena", 80);
        }

        var snapshot = new FollowupSuggestionSnapshot
        {
            SessionId = sessionId,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            IsSpeculative = request.Speculative,
            CacheStatus = "miss",
            Fingerprint = fingerprint,
            SuppressedReason = suggestions.Count == 0 ? "no_obvious_next_step" : string.Empty,
            Suggestions = suggestions
                .OrderByDescending(static item => item.Confidence)
                .ToArray()
        };

        if (!hasActiveTurn)
        {
            suggestionCache?.Set(sessionId, fingerprint, snapshot);
        }

        return SliceSnapshot(snapshot, maxCount, "miss");
    }

    private string ResolveSessionId(WorkspacePaths paths, string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId;
        }

        return transcriptStore.ListSessions(paths, 1).FirstOrDefault()?.SessionId ?? string.Empty;
    }

    private static string BuildFingerprint(
        DesktopSessionDetail detail,
        bool hasActiveTurn,
        bool hasRecoverableTurn,
        bool hasActiveArenaSession) =>
        string.Join(
            "|",
            detail.Session.SessionId,
            detail.Session.LastUpdatedAt,
            detail.Session.MessageCount,
            detail.Summary.PendingApprovalCount,
            detail.Summary.PendingQuestionCount,
            hasActiveTurn,
            hasRecoverableTurn,
            hasActiveArenaSession);

    private static FollowupSuggestionSnapshot SliceSnapshot(
        FollowupSuggestionSnapshot snapshot,
        int maxCount,
        string cacheStatus) =>
        new()
        {
            SessionId = snapshot.SessionId,
            SuppressedReason = snapshot.SuppressedReason,
            GeneratedAtUtc = snapshot.GeneratedAtUtc,
            IsSpeculative = snapshot.IsSpeculative,
            CacheStatus = cacheStatus,
            Fingerprint = snapshot.Fingerprint,
            Suggestions = snapshot.Suggestions.Take(maxCount).ToArray()
        };

    private static void AddSuggestion(
        IList<FollowupSuggestion> suggestions,
        string text,
        string kind,
        string source,
        int confidence)
    {
        if (suggestions.Any(item => string.Equals(item.Text, text, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        suggestions.Add(new FollowupSuggestion
        {
            Text = text,
            Kind = kind,
            Source = source,
            Confidence = confidence
        });
    }
}
