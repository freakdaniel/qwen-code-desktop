using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Sessions;

public interface ISessionEventFactory
{
    DesktopSessionEvent CreateTurnStarted(string sessionId, string prompt, string workingDirectory, string gitBranch, string toolName);

    DesktopSessionEvent CreateCommandCompleted(string sessionId, CommandInvocationResult commandInvocation, string workingDirectory, string gitBranch);

    DesktopSessionEvent CreateToolEvent(string sessionId, NativeToolExecutionResult toolExecution, string gitBranch);

    DesktopSessionEvent CreateAssistantRuntimeEvent(
        string sessionId,
        AssistantRuntimeEvent runtimeEvent,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string fallbackToolName);

    DesktopSessionEvent CreateAssistantCompleted(
        string sessionId,
        string assistantSummary,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName);

    DesktopSessionEvent CreateTurnReattached(
        string sessionId,
        string workingDirectory,
        string gitBranch,
        string toolName);

    DesktopSessionEvent CreateToolApproved(string sessionId, string toolName, string workingDirectory, string gitBranch, DateTime timestampUtc);

    DesktopSessionEvent CreateTurnCompleted(
        string sessionId,
        string message,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName,
        string status);

    DesktopSessionEvent CreateTurnCancelled(
        string sessionId,
        string message,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName);
}
