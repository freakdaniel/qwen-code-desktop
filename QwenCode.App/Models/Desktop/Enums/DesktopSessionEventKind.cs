namespace QwenCode.App.Models;

public enum DesktopSessionEventKind
{
    TurnStarted,
    CommandCompleted,
    ToolApprovalRequired,
    ToolCompleted,
    ToolBlocked,
    ToolFailed,
    ToolApproved,
    AssistantPreparingContext,
    AssistantGenerating,
    AssistantStreaming,
    AssistantCompleted,
    TurnInterrupted,
    TurnReattached,
    TurnCancelled,
    TurnCompleted
}
