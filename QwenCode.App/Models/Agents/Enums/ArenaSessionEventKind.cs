namespace QwenCode.App.Models;

public enum ArenaSessionEventKind
{
    SessionStarted,
    SessionUpdated,
    AgentStarted,
    AgentStatsUpdated,
    AgentCompleted,
    AgentFailed,
    RoundCompleted,
    SessionCancelled,
    SessionCompleted,
    SessionFailed,
    SessionRemoved
}
