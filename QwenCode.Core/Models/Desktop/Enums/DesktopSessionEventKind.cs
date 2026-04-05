namespace QwenCode.App.Models;

public enum DesktopSessionEventKind
{
    TurnStarted,
    CommandCompleted,
    ToolApprovalRequired,
    UserInputRequired,
    ToolCompleted,
    ToolBlocked,
    ToolFailed,
    ToolApproved,
    UserInputReceived,
    AssistantPreparingContext,
    AssistantGenerating,
    AssistantStreaming,
    AssistantCompleted,
    TurnInterrupted,
    TurnReattached,
    TurnCancelled,
    TurnCompleted
}
