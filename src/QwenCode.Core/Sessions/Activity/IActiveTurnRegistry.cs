using QwenCode.Core.Models;

namespace QwenCode.Core.Sessions;

/// <summary>
/// Defines the contract for Active Turn Registry
/// </summary>
public interface IActiveTurnRegistry
{
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
    Task<TResult> RunAsync<TResult>(
        string sessionId,
        ActiveTurnState initialState,
        Func<CancellationToken, Task<TResult>> operation,
        Func<Task<TResult>> onCancelled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool Cancel(string sessionId);

    /// <summary>
    /// Lists active turns
    /// </summary>
    /// <returns>The resulting i read only list active turn state</returns>
    IReadOnlyList<ActiveTurnState> ListActiveTurns();

    /// <summary>
    /// Updates value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="update">The update</param>
    void Update(string sessionId, Action<ActiveTurnState> update);
}
