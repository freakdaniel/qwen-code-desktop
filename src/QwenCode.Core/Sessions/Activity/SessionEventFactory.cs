using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Sessions;

/// <summary>
/// Represents the Session Event Factory
/// </summary>
public sealed class SessionEventFactory : ISessionEventFactory
{
    /// <summary>
    /// Creates turn started
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="prompt">The prompt content</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="toolName">The tool name</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateTurnStarted(
        string sessionId,
        string prompt,
        string workingDirectory,
        string gitBranch,
        string toolName) =>
        new()
        {
            SessionId = sessionId,
            Kind = DesktopSessionEventKind.TurnStarted,
            TimestampUtc = DateTime.UtcNow,
            Message = BuildTurnStartedMessage(prompt),
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            ToolName = toolName,
            Status = "started"
        };

    /// <summary>
    /// Creates command completed
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="commandInvocation">The command invocation</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateCommandCompleted(
        string sessionId,
        CommandInvocationResult commandInvocation,
        string workingDirectory,
        string gitBranch) =>
        new()
        {
            SessionId = sessionId,
            Kind = DesktopSessionEventKind.CommandCompleted,
            TimestampUtc = DateTime.UtcNow,
            Message = BuildCommandEventMessage(commandInvocation),
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            CommandName = commandInvocation.Command.Name,
            Status = commandInvocation.Status
        };

    /// <summary>
    /// Creates tool event
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="toolExecution">The tool execution</param>
    /// <param name="gitBranch">The git branch</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateToolEvent(string sessionId, NativeToolExecutionResult toolExecution, string gitBranch) =>
        new()
        {
            SessionId = sessionId,
            Kind = MapToolEventKind(toolExecution.Status),
            TimestampUtc = DateTime.UtcNow,
            Message = BuildToolEventMessage(toolExecution),
            WorkingDirectory = toolExecution.WorkingDirectory,
            GitBranch = gitBranch,
            ToolName = toolExecution.ToolName,
            Status = toolExecution.Status
        };

    /// <summary>
    /// Creates assistant runtime event
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="runtimeEvent">The runtime event</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="commandName">The command name</param>
    /// <param name="fallbackToolName">The fallback tool name</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateAssistantRuntimeEvent(
        string sessionId,
        AssistantRuntimeEvent runtimeEvent,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string fallbackToolName) =>
        new()
        {
            SessionId = sessionId,
            Kind = MapAssistantRuntimeEventKind(runtimeEvent.Stage),
            TimestampUtc = DateTime.UtcNow,
            Message = runtimeEvent.Message,
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            CommandName = commandName,
            ToolName = string.IsNullOrWhiteSpace(runtimeEvent.ToolName) ? fallbackToolName : runtimeEvent.ToolName,
            ToolCallId = runtimeEvent.ToolCallId,
            ToolCallGroupId = runtimeEvent.ToolCallGroupId,
            ToolArgumentsJson = runtimeEvent.ToolArgumentsJson,
            Status = string.IsNullOrWhiteSpace(runtimeEvent.Status) ? runtimeEvent.Stage : runtimeEvent.Status,
            ContentDelta = runtimeEvent.ContentDelta,
            ContentSnapshot = runtimeEvent.ContentSnapshot,
            AgentName = runtimeEvent.AgentName
        };

    /// <summary>
    /// Creates assistant completed
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="assistantSummary">The assistant summary</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="commandName">The command name</param>
    /// <param name="toolName">The tool name</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateAssistantCompleted(
        string sessionId,
        string assistantSummary,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName) =>
        new()
        {
            SessionId = sessionId,
            Kind = DesktopSessionEventKind.AssistantCompleted,
            TimestampUtc = DateTime.UtcNow,
            Message = assistantSummary,
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            CommandName = commandName,
            ToolName = toolName,
            Status = "completed"
        };

    /// <summary>
    /// Creates turn reattached
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="toolName">The tool name</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateTurnReattached(
        string sessionId,
        string workingDirectory,
        string gitBranch,
        string toolName) =>
        new()
        {
            SessionId = sessionId,
            Kind = DesktopSessionEventKind.TurnReattached,
            TimestampUtc = DateTime.UtcNow,
            Message = "Recovered an interrupted turn and attached it to the current run.",
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            ToolName = toolName,
            Status = "reattached"
        };

    /// <summary>
    /// Creates tool approved
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="timestampUtc">The timestamp utc</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateToolApproved(
        string sessionId,
        string toolName,
        string workingDirectory,
        string gitBranch,
        DateTime timestampUtc) =>
        new()
        {
            SessionId = sessionId,
            Kind = DesktopSessionEventKind.ToolApproved,
            TimestampUtc = timestampUtc,
            Message = $"Approved tool '{toolName}'.",
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            ToolName = toolName,
            Status = "approved"
        };

    /// <summary>
    /// Creates user input received
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="timestampUtc">The timestamp utc</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateUserInputReceived(
        string sessionId,
        string toolName,
        string workingDirectory,
        string gitBranch,
        DateTime timestampUtc) =>
        new()
        {
            SessionId = sessionId,
            Kind = DesktopSessionEventKind.UserInputReceived,
            TimestampUtc = timestampUtc,
            Message = $"Captured answers for tool '{toolName}'.",
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            ToolName = toolName,
            Status = "answered"
        };

    /// <summary>
    /// Creates turn completed
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="message">The message</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="commandName">The command name</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="status">The status</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateTurnCompleted(
        string sessionId,
        string message,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName,
        string status) =>
        new()
        {
            SessionId = sessionId,
            Kind = DesktopSessionEventKind.TurnCompleted,
            TimestampUtc = DateTime.UtcNow,
            Message = message,
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            CommandName = commandName,
            ToolName = toolName,
            Status = status
        };

    /// <summary>
    /// Creates turn cancelled
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="message">The message</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="commandName">The command name</param>
    /// <param name="toolName">The tool name</param>
    /// <returns>The resulting desktop session event</returns>
    public DesktopSessionEvent CreateTurnCancelled(
        string sessionId,
        string message,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName) =>
        new()
        {
            SessionId = sessionId,
            Kind = DesktopSessionEventKind.TurnCancelled,
            TimestampUtc = DateTime.UtcNow,
            Message = message,
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            CommandName = commandName,
            ToolName = toolName,
            Status = "cancelled"
        };

    private static DesktopSessionEventKind MapAssistantRuntimeEventKind(string stage) =>
        stage switch
        {
            "assembling-context" => DesktopSessionEventKind.AssistantPreparingContext,
            "response-delta" => DesktopSessionEventKind.AssistantStreaming,
            "tool-approval-required" => DesktopSessionEventKind.ToolApprovalRequired,
            "user-input-required" => DesktopSessionEventKind.UserInputRequired,
            "tool-blocked" => DesktopSessionEventKind.ToolBlocked,
            "tool-failed" => DesktopSessionEventKind.ToolFailed,
            "tool-completed" => DesktopSessionEventKind.ToolCompleted,
            _ => DesktopSessionEventKind.AssistantGenerating
        };

    private static string BuildTurnStartedMessage(string prompt)
    {
        var normalized = prompt.Trim();
        if (normalized.Length <= 96)
        {
            return $"Starting desktop turn: {normalized}";
        }

        return $"Starting desktop turn: {normalized[..96]}...";
    }

    private static string BuildCommandEventMessage(CommandInvocationResult commandInvocation) =>
        commandInvocation.Status switch
        {
            "completed" => $"Built-in command '/{commandInvocation.Command.Name}' completed.",
            "resolved" => $"Slash command '/{commandInvocation.Command.Name}' resolved.",
            "error" => $"Command '/{commandInvocation.Command.Name}' failed: {commandInvocation.ErrorMessage}",
            _ => $"Command '/{commandInvocation.Command.Name}' updated the session."
        };

    private static DesktopSessionEventKind MapToolEventKind(string status) =>
        status switch
        {
            "approval-required" => DesktopSessionEventKind.ToolApprovalRequired,
            "input-required" => DesktopSessionEventKind.UserInputRequired,
            "completed" => DesktopSessionEventKind.ToolCompleted,
            "blocked" => DesktopSessionEventKind.ToolBlocked,
            "error" => DesktopSessionEventKind.ToolFailed,
            _ => DesktopSessionEventKind.ToolCompleted
        };

    private static string BuildToolEventMessage(NativeToolExecutionResult toolExecution) =>
        toolExecution.Status switch
        {
            "approval-required" => $"Tool '{toolExecution.ToolName}' is waiting for approval.",
            "input-required" => $"Tool '{toolExecution.ToolName}' is waiting for user answers.",
            "completed" => $"Tool '{toolExecution.ToolName}' completed.",
            "blocked" => $"Tool '{toolExecution.ToolName}' was blocked by approval policy.",
            "error" => $"Tool '{toolExecution.ToolName}' failed: {toolExecution.ErrorMessage}",
            _ => $"Tool '{toolExecution.ToolName}' updated the session."
        };
}
