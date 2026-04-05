namespace QwenCode.App.Models;

public enum HookEventName
{
    UserPromptSubmit,
    Stop,
    SessionStart,
    SessionEnd,
    PreToolUse,
    PostToolUse,
    PostToolUseFailure,
    SubagentStart,
    SubagentStop,
    Notification,
    PermissionRequest,
    PreCompact
}
