namespace QwenCode.App.Models;

/// <summary>
/// Specifies the available Arena Session Event Kind
/// </summary>
public enum ArenaSessionEventKind
{
    /// <summary>
    /// Represents the Session Started option
    /// </summary>
    SessionStarted,
    /// <summary>
    /// Represents the Session Updated option
    /// </summary>
    SessionUpdated,
    /// <summary>
    /// Represents the Agent Started option
    /// </summary>
    AgentStarted,
    /// <summary>
    /// Represents the Agent Stats Updated option
    /// </summary>
    AgentStatsUpdated,
    /// <summary>
    /// Represents the Agent Completed option
    /// </summary>
    AgentCompleted,
    /// <summary>
    /// Represents the Agent Failed option
    /// </summary>
    AgentFailed,
    /// <summary>
    /// Represents the Round Completed option
    /// </summary>
    RoundCompleted,
    /// <summary>
    /// Represents the Session Cancelled option
    /// </summary>
    SessionCancelled,
    /// <summary>
    /// Represents the Session Completed option
    /// </summary>
    SessionCompleted,
    /// <summary>
    /// Represents the Session Failed option
    /// </summary>
    SessionFailed,
    /// <summary>
    /// Represents the Session Removed option
    /// </summary>
    SessionRemoved
}
