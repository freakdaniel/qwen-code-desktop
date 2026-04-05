using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface IActiveTurnRegistry
{
    Task<TResult> RunAsync<TResult>(
        string sessionId,
        ActiveTurnState initialState,
        Func<CancellationToken, Task<TResult>> operation,
        Func<Task<TResult>> onCancelled,
        CancellationToken cancellationToken = default);

    bool Cancel(string sessionId);

    IReadOnlyList<ActiveTurnState> ListActiveTurns();

    void Update(string sessionId, Action<ActiveTurnState> update);
}
