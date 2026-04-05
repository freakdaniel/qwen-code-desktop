using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Sessions;

public sealed class SessionEventFactory : ISessionEventFactory
{
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
            Status = string.IsNullOrWhiteSpace(runtimeEvent.Status) ? runtimeEvent.Stage : runtimeEvent.Status,
            ContentDelta = runtimeEvent.ContentDelta,
            ContentSnapshot = runtimeEvent.ContentSnapshot,
            AgentName = runtimeEvent.AgentName
        };

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
            Message = "Recovered an interrupted desktop turn and attached it to a new runtime execution.",
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            ToolName = toolName,
            Status = "reattached"
        };

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
            Message = $"Approved native tool '{toolName}' for execution.",
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            ToolName = toolName,
            Status = "approved"
        };

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
            Message = $"Captured user answers for native tool '{toolName}'.",
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            ToolName = toolName,
            Status = "answered"
        };

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
            "approval-required" => $"Native tool '{toolExecution.ToolName}' is waiting for approval.",
            "input-required" => $"Native tool '{toolExecution.ToolName}' is waiting for user answers.",
            "completed" => $"Native tool '{toolExecution.ToolName}' completed inside the .NET host.",
            "blocked" => $"Native tool '{toolExecution.ToolName}' was blocked by approval policy.",
            "error" => $"Native tool '{toolExecution.ToolName}' failed: {toolExecution.ErrorMessage}",
            _ => $"Native tool '{toolExecution.ToolName}' updated the session."
        };
}
