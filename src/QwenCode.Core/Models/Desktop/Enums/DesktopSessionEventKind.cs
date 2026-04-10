namespace QwenCode.Core.Models;

/// <summary>
/// Specifies the available Desktop Session Event Kind
/// </summary>
public enum DesktopSessionEventKind
{
    /// <summary>
    /// Represents the Turn Started option
    /// </summary>
    TurnStarted,
    /// <summary>
    /// Represents the Command Completed option
    /// </summary>
    CommandCompleted,
    /// <summary>
    /// Represents the Tool Approval Required option
    /// </summary>
    ToolApprovalRequired,
    /// <summary>
    /// Represents the User Input Required option
    /// </summary>
    UserInputRequired,
    /// <summary>
    /// Represents the Tool Completed option
    /// </summary>
    ToolCompleted,
    /// <summary>
    /// Represents the Tool Blocked option
    /// </summary>
    ToolBlocked,
    /// <summary>
    /// Represents the Tool Failed option
    /// </summary>
    ToolFailed,
    /// <summary>
    /// Represents the Tool Approved option
    /// </summary>
    ToolApproved,
    /// <summary>
    /// Represents the User Input Received option
    /// </summary>
    UserInputReceived,
    /// <summary>
    /// Represents the Assistant Preparing Context option
    /// </summary>
    AssistantPreparingContext,
    /// <summary>
    /// Represents the Assistant Generating option
    /// </summary>
    AssistantGenerating,
    /// <summary>
    /// Represents the Assistant Streaming option
    /// </summary>
    AssistantStreaming,
    /// <summary>
    /// Represents the Assistant Completed option
    /// </summary>
    AssistantCompleted,
    /// <summary>
    /// Represents the Turn Interrupted option
    /// </summary>
    TurnInterrupted,
    /// <summary>
    /// Represents the Turn Reattached option
    /// </summary>
    TurnReattached,
    /// <summary>
    /// Represents the Turn Cancelled option
    /// </summary>
    TurnCancelled,
    /// <summary>
    /// Represents the Turn Completed option
    /// </summary>
    TurnCompleted,
    /// <summary>
    /// Represents the Session Title Updated option
    /// </summary>
    SessionTitleUpdated
}
