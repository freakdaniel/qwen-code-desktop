using QwenCode.App.Models;

namespace QwenCode.App.Agents;

public interface IArenaSessionRegistry
{
    event EventHandler<ArenaSessionEvent>? SessionEvent;

    void Start(ActiveArenaSessionState initialState, CancellationTokenSource tokenSource, string message);

    void Update(
        string sessionId,
        Action<ActiveArenaSessionState> update,
        ArenaSessionEventKind kind,
        string message,
        string agentName = "");

    void Complete(
        string sessionId,
        string status,
        int roundCount,
        string selectedWinner,
        ArenaSessionStats stats,
        IReadOnlyList<ArenaAgentStatusFile> agents,
        string message);

    bool Cancel(string sessionId, string message);

    void Remove(string sessionId, string message);

    IReadOnlyList<ActiveArenaSessionState> ListActiveSessions();
}
