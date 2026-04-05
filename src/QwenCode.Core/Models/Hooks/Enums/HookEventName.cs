namespace QwenCode.App.Models;

/// <summary>
/// Specifies the available Hook Event Name
/// </summary>
public enum HookEventName
{
    /// <summary>
    /// Represents the User Prompt Submit option
    /// </summary>
    UserPromptSubmit,
    /// <summary>
    /// Represents the Stop option
    /// </summary>
    Stop,
    /// <summary>
    /// Represents the Session Start option
    /// </summary>
    SessionStart,
    /// <summary>
    /// Represents the Session End option
    /// </summary>
    SessionEnd,
    /// <summary>
    /// Represents the Pre Tool Use option
    /// </summary>
    PreToolUse,
    /// <summary>
    /// Represents the Post Tool Use option
    /// </summary>
    PostToolUse,
    /// <summary>
    /// Represents the Post Tool Use Failure option
    /// </summary>
    PostToolUseFailure,
    /// <summary>
    /// Represents the Subagent Start option
    /// </summary>
    SubagentStart,
    /// <summary>
    /// Represents the Subagent Stop option
    /// </summary>
    SubagentStop,
    /// <summary>
    /// Represents the Notification option
    /// </summary>
    Notification,
    /// <summary>
    /// Represents the Permission Request option
    /// </summary>
    PermissionRequest,
    /// <summary>
    /// Represents the Pre Compact option
    /// </summary>
    PreCompact
}
