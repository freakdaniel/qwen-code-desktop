namespace QwenCode.Core.Runtime;

/// <summary>
/// Describes high-level prompt modes for the native assistant runtime.
/// </summary>
public enum AssistantPromptMode
{
    /// <summary>
    /// Default interactive coding assistant mode.
    /// </summary>
    Primary = 0,

    /// <summary>
    /// Structured planning mode.
    /// </summary>
    Plan = 1,

    /// <summary>
    /// Follow-up suggestion generation mode.
    /// </summary>
    FollowupSuggestion = 2,

    /// <summary>
    /// Headless delegated subagent mode.
    /// </summary>
    Subagent = 3,

    /// <summary>
    /// Arena competitor mode.
    /// </summary>
    ArenaCompetitor = 4
}
