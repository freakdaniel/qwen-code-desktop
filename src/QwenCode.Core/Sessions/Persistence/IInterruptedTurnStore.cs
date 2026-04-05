using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

/// <summary>
/// Defines the contract for Interrupted Turn Store
/// </summary>
public interface IInterruptedTurnStore
{
    /// <summary>
    /// Executes upsert
    /// </summary>
    /// <param name="state">The state</param>
    void Upsert(ActiveTurnState state);

    /// <summary>
    /// Gets value
    /// </summary>
    /// <param name="chatsDirectory">The chats directory</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>The resulting interrupted turn state?</returns>
    InterruptedTurnState? Get(string chatsDirectory, string sessionId);

    /// <summary>
    /// Lists recoverable turns
    /// </summary>
    /// <param name="chatsDirectory">The chats directory</param>
    /// <returns>The resulting i read only list recoverable turn state</returns>
    IReadOnlyList<RecoverableTurnState> ListRecoverableTurns(string chatsDirectory);

    /// <summary>
    /// Removes value
    /// </summary>
    /// <param name="chatsDirectory">The chats directory</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool Remove(string chatsDirectory, string sessionId);
}
