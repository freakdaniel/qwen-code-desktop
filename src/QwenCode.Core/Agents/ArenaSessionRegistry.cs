using System.Collections.Concurrent;
using QwenCode.Core.Models;
using QwenCode.Core.Runtime;

namespace QwenCode.Core.Agents;

/// <summary>
/// Represents the Arena Session Registry
/// </summary>
public sealed class ArenaSessionRegistry : IArenaSessionRegistry
{
    private sealed class Entry
    {
        /// <summary>
        /// Gets or sets the state
        /// </summary>
        public required ActiveArenaSessionState State { get; set; }

        /// <summary>
        /// Gets or sets the token source
        /// </summary>
        public required CancellationTokenSource TokenSource { get; init; }

        /// <summary>
        /// Gets the sync root
        /// </summary>
        public object SyncRoot { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    public event EventHandler<ArenaSessionEvent>? SessionEvent;

    /// <summary>
    /// Starts value
    /// </summary>
    /// <param name="initialState">The initial state</param>
    /// <param name="tokenSource">The token source</param>
    /// <param name="message">The message</param>
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
            TaskId = initialState.TaskId,
            Status = initialState.Status,
            Message = message,
            RoundCount = initialState.RoundCount,
            SelectedWinner = initialState.SelectedWinner,
            Stats = Clone(initialState.Stats),
            TimestampUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Updates value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="update">The update</param>
    /// <param name="kind">The kind</param>
    /// <param name="message">The message</param>
    /// <param name="agentName">The agent name</param>
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
            TaskId = snapshot.TaskId,
            Status = snapshot.Status,
            Message = message,
            AgentName = agentName,
            RoundCount = snapshot.RoundCount,
            SelectedWinner = snapshot.SelectedWinner,
            Stats = Clone(snapshot.Stats),
            TimestampUtc = snapshot.LastUpdatedAtUtc
        });
    }

    /// <summary>
    /// Executes complete
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="taskId">The linked orchestration task id</param>
    /// <param name="status">The status</param>
    /// <param name="roundCount">The round count</param>
    /// <param name="selectedWinner">The selected winner</param>
    /// <param name="stats">The stats</param>
    /// <param name="agents">The agents</param>
    /// <param name="message">The message</param>
    public void Complete(
        string sessionId,
        string taskId,
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
                TaskId = string.IsNullOrWhiteSpace(taskId) ? completed.TaskId : taskId,
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
            TaskId = snapshot.TaskId,
            Status = snapshot.Status,
            Message = message,
            RoundCount = snapshot.RoundCount,
            SelectedWinner = snapshot.SelectedWinner,
            Stats = Clone(snapshot.Stats),
            TimestampUtc = snapshot.LastUpdatedAtUtc
        });
    }

    /// <summary>
    /// Cancels value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="message">The message</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
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
            TaskId = snapshot.TaskId,
            Status = snapshot.Status,
            Message = message,
            RoundCount = snapshot.RoundCount,
            SelectedWinner = snapshot.SelectedWinner,
            Stats = Clone(snapshot.Stats),
            TimestampUtc = snapshot.LastUpdatedAtUtc
        });

        return true;
    }

    /// <summary>
    /// Removes value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="message">The message</param>
    public void Remove(string sessionId, string message)
    {
        if (!_sessions.TryRemove(sessionId, out var entry))
        {
            return;
        }

        ActiveArenaSessionState snapshot;
        lock (entry.SyncRoot)
        {
            snapshot = Clone(entry.State);
        }

        Publish(new ArenaSessionEvent
        {
            SessionId = snapshot.SessionId,
            Kind = ArenaSessionEventKind.SessionRemoved,
            TaskId = snapshot.TaskId,
            Status = "removed",
            Message = message,
            TimestampUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Lists active sessions
    /// </summary>
    /// <returns>The resulting i read only list active arena session state</returns>
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
            TaskId = state.TaskId,
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
            TaskId = state.TaskId,
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
