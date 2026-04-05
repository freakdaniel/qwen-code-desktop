using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface IInterruptedTurnStore
{
    void Upsert(ActiveTurnState state);

    InterruptedTurnState? Get(string chatsDirectory, string sessionId);

    IReadOnlyList<RecoverableTurnState> ListRecoverableTurns(string chatsDirectory);

    bool Remove(string chatsDirectory, string sessionId);
}
