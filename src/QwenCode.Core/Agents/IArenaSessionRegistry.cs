using QwenCode.Core.Models;

namespace QwenCode.Core.Agents;

/// <summary>
/// Defines the contract for Arena Session Registry
/// </summary>
public interface IArenaSessionRegistry
{
    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    event EventHandler<ArenaSessionEvent>? SessionEvent;

    /// <summary>
    /// Starts value
    /// </summary>
    /// <param name="initialState">The initial state</param>
    /// <param name="tokenSource">The token source</param>
    /// <param name="message">The message</param>
    void Start(ActiveArenaSessionState initialState, CancellationTokenSource tokenSource, string message);

    /// <summary>
    /// Updates value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="update">The update</param>
    /// <param name="kind">The kind</param>
    /// <param name="message">The message</param>
    /// <param name="agentName">The agent name</param>
    void Update(
        string sessionId,
        Action<ActiveArenaSessionState> update,
        ArenaSessionEventKind kind,
        string message,
        string agentName = "");

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
    void Complete(
        string sessionId,
        string taskId,
        string status,
        int roundCount,
        string selectedWinner,
        ArenaSessionStats stats,
        IReadOnlyList<ArenaAgentStatusFile> agents,
        string message);

    /// <summary>
    /// Cancels value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="message">The message</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool Cancel(string sessionId, string message);

    /// <summary>
    /// Removes value
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="message">The message</param>
    void Remove(string sessionId, string message);

    /// <summary>
    /// Lists active sessions
    /// </summary>
    /// <returns>The resulting i read only list active arena session state</returns>
    IReadOnlyList<ActiveArenaSessionState> ListActiveSessions();
}
