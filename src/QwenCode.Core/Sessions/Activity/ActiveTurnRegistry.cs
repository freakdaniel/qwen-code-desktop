using QwenCode.Core.Models;

namespace QwenCode.Core.Sessions;

/// <summary>
/// Represents the Active Turn Registry
/// </summary>
public sealed class ActiveTurnRegistry : IActiveTurnRegistry
{
    private sealed class ActiveTurnEntry
    {
        /// <summary>
        /// Gets or sets the token source
        /// </summary>
        public required CancellationTokenSource TokenSource { get; init; }

        /// <summary>
        /// Gets or sets the state
        /// </summary>
        public required ActiveTurnState State { get; init; }

        /// <summary>
        /// Gets the sync root
        /// </summary>
        public object SyncRoot { get; } = new();
    }

    private readonly ConcurrentDictionary<string, ActiveTurnEntry> _activeTurns = new(StringComparer.Ordinal);
    private readonly IInterruptedTurnStore? _interruptedTurnStore;

    /// <summary>
    /// Initializes a new instance of the ActiveTurnRegistry class
    /// </summary>
    public ActiveTurnRegistry()
    {
    }

    /// <summary>
    /// Initializes a new instance of the ActiveTurnRegistry class
    /// </summary>
    /// <param name="interruptedTurnStore">The interrupted turn store</param>
    public ActiveTurnRegistry(IInterruptedTurnStore interruptedTurnStore)
    {
        _interruptedTurnStore = interruptedTurnStore;
    }

    /// <summary>
    /// Executes run async
    /// </summary>
    /// <typeparam name="TResult">The type of t result</typeparam>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="initialState">The initial state</param>
    /// <param name="operation">The operation</param>
    /// <param name="onCancelled">The on cancelled</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to t result</returns>
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

    /// <summary>
    /// Cancels value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool Cancel(string sessionId)
    {
        if (!_activeTurns.TryGetValue(sessionId, out var entry))
        {
            return false;
        }

        entry.TokenSource.Cancel();
        return true;
    }

    /// <summary>
    /// Lists active turns
    /// </summary>
    /// <returns>The resulting i read only list active turn state</returns>
    public IReadOnlyList<ActiveTurnState> ListActiveTurns() =>
        _activeTurns.Values
            .Select(CloneEntryState)
            .OrderBy(static state => state.StartedAtUtc)
            .ToArray();

    /// <summary>
    /// Updates value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="update">The update</param>
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
            ThinkingSnapshot = state.ThinkingSnapshot,
            StartedAtUtc = state.StartedAtUtc,
            LastUpdatedAtUtc = state.LastUpdatedAtUtc
        };
}
