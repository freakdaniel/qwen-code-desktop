using System.Collections.Concurrent;
using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public sealed class ActiveTurnRegistry : IActiveTurnRegistry
{
    private sealed class ActiveTurnEntry
    {
        public required CancellationTokenSource TokenSource { get; init; }

        public required ActiveTurnState State { get; init; }

        public object SyncRoot { get; } = new();
    }

    private readonly ConcurrentDictionary<string, ActiveTurnEntry> _activeTurns = new(StringComparer.Ordinal);
    private readonly IInterruptedTurnStore? _interruptedTurnStore;

    public ActiveTurnRegistry()
    {
    }

    public ActiveTurnRegistry(IInterruptedTurnStore interruptedTurnStore)
    {
        _interruptedTurnStore = interruptedTurnStore;
    }

    public async Task<TResult> RunAsync<TResult>(
        string sessionId,
        ActiveTurnState initialState,
        Func<CancellationToken, Task<TResult>> operation,
        Func<Task<TResult>> onCancelled,
        CancellationToken cancellationToken = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var entry = new ActiveTurnEntry
        {
            TokenSource = linkedCts,
            State = Clone(initialState)
        };

        if (!_activeTurns.TryAdd(sessionId, entry))
        {
            linkedCts.Dispose();
            throw new InvalidOperationException("Another active desktop turn is already running for this session.");
        }

        Persist(entry.State);

        try
        {
            return await operation(linkedCts.Token);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            return await onCancelled();
        }
        finally
        {
            _activeTurns.TryRemove(sessionId, out _);
            RemovePersistedState(entry.State);
            linkedCts.Dispose();
        }
    }

    public bool Cancel(string sessionId)
    {
        if (!_activeTurns.TryGetValue(sessionId, out var entry))
        {
            return false;
        }

        entry.TokenSource.Cancel();
        return true;
    }

    public IReadOnlyList<ActiveTurnState> ListActiveTurns() =>
        _activeTurns.Values
            .Select(CloneEntryState)
            .OrderBy(static state => state.StartedAtUtc)
            .ToArray();

    public void Update(string sessionId, Action<ActiveTurnState> update)
    {
        if (!_activeTurns.TryGetValue(sessionId, out var entry))
        {
            return;
        }

        lock (entry.SyncRoot)
        {
            update(entry.State);
            entry.State.LastUpdatedAtUtc = DateTime.UtcNow;
            Persist(entry.State);
        }
    }

    private void Persist(ActiveTurnState state)
    {
        _interruptedTurnStore?.Upsert(Clone(state));
    }

    private void RemovePersistedState(ActiveTurnState state)
    {
        if (_interruptedTurnStore is null)
        {
            return;
        }

        var chatsDirectory = Path.GetDirectoryName(state.TranscriptPath);
        if (!string.IsNullOrWhiteSpace(chatsDirectory))
        {
            _interruptedTurnStore.Remove(chatsDirectory, state.SessionId);
        }
    }

    private static ActiveTurnState CloneEntryState(ActiveTurnEntry entry)
    {
        lock (entry.SyncRoot)
        {
            return Clone(entry.State);
        }
    }

    private static ActiveTurnState Clone(ActiveTurnState state) =>
        new()
        {
            SessionId = state.SessionId,
            Prompt = state.Prompt,
            TranscriptPath = state.TranscriptPath,
            WorkingDirectory = state.WorkingDirectory,
            GitBranch = state.GitBranch,
            ToolName = state.ToolName,
            Stage = state.Stage,
            Status = state.Status,
            ContentSnapshot = state.ContentSnapshot,
            StartedAtUtc = state.StartedAtUtc,
            LastUpdatedAtUtc = state.LastUpdatedAtUtc
        };
}
