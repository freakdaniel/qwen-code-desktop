using System.Collections.Concurrent;
using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Agents;

public sealed class ArenaSessionRegistry : IArenaSessionRegistry
{
    private sealed class Entry
    {
        public required ActiveArenaSessionState State { get; set; }

        public required CancellationTokenSource TokenSource { get; init; }

        public object SyncRoot { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);

    public event EventHandler<ArenaSessionEvent>? SessionEvent;

    public void Start(ActiveArenaSessionState initialState, CancellationTokenSource tokenSource, string message)
    {
        var entry = new Entry
        {
            State = Clone(initialState),
            TokenSource = tokenSource
        };

        if (!_sessions.TryAdd(initialState.SessionId, entry))
        {
            throw new InvalidOperationException($"Arena session '{initialState.SessionId}' is already active.");
        }

        Publish(new ArenaSessionEvent
        {
            SessionId = initialState.SessionId,
            Kind = ArenaSessionEventKind.SessionStarted,
            Status = initialState.Status,
            Message = message,
            RoundCount = initialState.RoundCount,
            SelectedWinner = initialState.SelectedWinner,
            Stats = Clone(initialState.Stats),
            TimestampUtc = DateTime.UtcNow
        });
    }

    public void Update(
        string sessionId,
        Action<ActiveArenaSessionState> update,
        ArenaSessionEventKind kind,
        string message,
        string agentName = "")
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            return;
        }

        ActiveArenaSessionState snapshot;
        lock (entry.SyncRoot)
        {
            var mutable = Clone(entry.State);
            update(mutable);
            mutable = CloneWithTimestamp(mutable, DateTime.UtcNow);
            entry.State = mutable;
            snapshot = Clone(mutable);
        }

        Publish(new ArenaSessionEvent
        {
            SessionId = snapshot.SessionId,
            Kind = kind,
            Status = snapshot.Status,
            Message = message,
            AgentName = agentName,
            RoundCount = snapshot.RoundCount,
            SelectedWinner = snapshot.SelectedWinner,
            Stats = Clone(snapshot.Stats),
            TimestampUtc = snapshot.LastUpdatedAtUtc
        });
    }

    public void Complete(
        string sessionId,
        string status,
        int roundCount,
        string selectedWinner,
        ArenaSessionStats stats,
        IReadOnlyList<ArenaAgentStatusFile> agents,
        string message)
    {
        if (!_sessions.TryRemove(sessionId, out var entry))
        {
            return;
        }

        ActiveArenaSessionState snapshot;
        lock (entry.SyncRoot)
        {
            var completed = Clone(entry.State);
            completed = new ActiveArenaSessionState
            {
                SessionId = completed.SessionId,
                Task = completed.Task,
                Status = status,
                WorkingDirectory = completed.WorkingDirectory,
                BaseBranch = completed.BaseBranch,
                RoundCount = roundCount,
                SelectedWinner = selectedWinner,
                StartedAtUtc = completed.StartedAtUtc,
                LastUpdatedAtUtc = DateTime.UtcNow,
                Stats = Clone(stats),
                Agents = agents.Select(Clone).ToArray()
            };
            snapshot = completed;
        }

        Publish(new ArenaSessionEvent
        {
            SessionId = snapshot.SessionId,
            Kind = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, "idle", StringComparison.OrdinalIgnoreCase)
                ? ArenaSessionEventKind.SessionCompleted
                : string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
                    ? ArenaSessionEventKind.SessionCancelled
                    : ArenaSessionEventKind.SessionFailed,
            Status = snapshot.Status,
            Message = message,
            RoundCount = snapshot.RoundCount,
            SelectedWinner = snapshot.SelectedWinner,
            Stats = Clone(snapshot.Stats),
            TimestampUtc = snapshot.LastUpdatedAtUtc
        });
    }

    public bool Cancel(string sessionId, string message)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            return false;
        }

        ActiveArenaSessionState snapshot;
        lock (entry.SyncRoot)
        {
            entry.State.Status = "cancelling";
            entry.State.LastUpdatedAtUtc = DateTime.UtcNow;
            snapshot = Clone(entry.State);
        }

        entry.TokenSource.Cancel();

        Publish(new ArenaSessionEvent
        {
            SessionId = snapshot.SessionId,
            Kind = ArenaSessionEventKind.SessionUpdated,
            Status = snapshot.Status,
            Message = message,
            RoundCount = snapshot.RoundCount,
            SelectedWinner = snapshot.SelectedWinner,
            Stats = Clone(snapshot.Stats),
            TimestampUtc = snapshot.LastUpdatedAtUtc
        });

        return true;
    }

    public void Remove(string sessionId, string message)
    {
        if (!_sessions.TryRemove(sessionId, out _))
        {
            return;
        }

        Publish(new ArenaSessionEvent
        {
            SessionId = sessionId,
            Kind = ArenaSessionEventKind.SessionRemoved,
            Status = "removed",
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }

    public IReadOnlyList<ActiveArenaSessionState> ListActiveSessions() =>
        _sessions.Values
            .Select(static entry =>
            {
                lock (entry.SyncRoot)
                {
                    return Clone(entry.State);
                }
            })
            .OrderBy(static item => item.StartedAtUtc)
            .ToArray();

    private void Publish(ArenaSessionEvent sessionEvent)
    {
        SessionEvent?.Invoke(this, sessionEvent);
    }

    private static ActiveArenaSessionState CloneWithTimestamp(ActiveArenaSessionState state, DateTime timestampUtc) =>
        new()
        {
            SessionId = state.SessionId,
            Task = state.Task,
            Status = state.Status,
            WorkingDirectory = state.WorkingDirectory,
            BaseBranch = state.BaseBranch,
            RoundCount = state.RoundCount,
            SelectedWinner = state.SelectedWinner,
            StartedAtUtc = state.StartedAtUtc,
            LastUpdatedAtUtc = timestampUtc,
            Stats = Clone(state.Stats),
            Agents = state.Agents.Select(Clone).ToArray()
        };

    private static ActiveArenaSessionState Clone(ActiveArenaSessionState state) =>
        new()
        {
            SessionId = state.SessionId,
            Task = state.Task,
            Status = state.Status,
            WorkingDirectory = state.WorkingDirectory,
            BaseBranch = state.BaseBranch,
            RoundCount = state.RoundCount,
            SelectedWinner = state.SelectedWinner,
            StartedAtUtc = state.StartedAtUtc,
            LastUpdatedAtUtc = state.LastUpdatedAtUtc,
            Stats = Clone(state.Stats),
            Agents = state.Agents.Select(Clone).ToArray()
        };

    private static ArenaSessionStats Clone(ArenaSessionStats stats) =>
        new()
        {
            AgentCount = stats.AgentCount,
            CompletedAgentCount = stats.CompletedAgentCount,
            FailedAgentCount = stats.FailedAgentCount,
            RoundCount = stats.RoundCount,
            ToolCallCount = stats.ToolCallCount,
            SuccessfulToolCallCount = stats.SuccessfulToolCallCount,
            FailedToolCallCount = stats.FailedToolCallCount,
            TotalDurationMs = stats.TotalDurationMs
        };

    private static ArenaAgentStatusFile Clone(ArenaAgentStatusFile state) =>
        new()
        {
            AgentId = state.AgentId,
            AgentName = state.AgentName,
            Status = state.Status,
            Model = state.Model,
            StopReason = state.StopReason,
            Stats = new AssistantExecutionStats
            {
                RoundCount = state.Stats.RoundCount,
                ToolCallCount = state.Stats.ToolCallCount,
                SuccessfulToolCallCount = state.Stats.SuccessfulToolCallCount,
                FailedToolCallCount = state.Stats.FailedToolCallCount,
                DurationMs = state.Stats.DurationMs
            },
            WorktreeName = state.WorktreeName,
            WorktreePath = state.WorktreePath,
            Branch = state.Branch,
            ProviderName = state.ProviderName,
            CurrentActivity = state.CurrentActivity,
            FinalSummary = state.FinalSummary,
            Error = state.Error,
            UpdatedAtUtc = state.UpdatedAtUtc
        };
}
