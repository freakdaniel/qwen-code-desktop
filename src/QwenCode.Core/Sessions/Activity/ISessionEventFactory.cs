using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Sessions;

/// <summary>
/// Defines the contract for Session Event Factory
/// </summary>
public interface ISessionEventFactory
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
    DesktopSessionEvent CreateTurnStarted(string sessionId, string prompt, string workingDirectory, string gitBranch, string toolName);

    /// <summary>
    /// Creates command completed
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="commandInvocation">The command invocation</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <returns>The resulting desktop session event</returns>
    DesktopSessionEvent CreateCommandCompleted(string sessionId, CommandInvocationResult commandInvocation, string workingDirectory, string gitBranch);

    /// <summary>
    /// Creates tool event
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="toolExecution">The tool execution</param>
    /// <param name="gitBranch">The git branch</param>
    /// <returns>The resulting desktop session event</returns>
    DesktopSessionEvent CreateToolEvent(string sessionId, NativeToolExecutionResult toolExecution, string gitBranch);

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
    DesktopSessionEvent CreateAssistantRuntimeEvent(
        string sessionId,
        AssistantRuntimeEvent runtimeEvent,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string fallbackToolName);

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
    DesktopSessionEvent CreateAssistantCompleted(
        string sessionId,
        string assistantSummary,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName);

    /// <summary>
    /// Creates turn reattached
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="toolName">The tool name</param>
    /// <returns>The resulting desktop session event</returns>
    DesktopSessionEvent CreateTurnReattached(
        string sessionId,
        string workingDirectory,
        string gitBranch,
        string toolName);

    /// <summary>
    /// Creates tool approved
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="timestampUtc">The timestamp utc</param>
    /// <returns>The resulting desktop session event</returns>
    DesktopSessionEvent CreateToolApproved(string sessionId, string toolName, string workingDirectory, string gitBranch, DateTime timestampUtc);

    /// <summary>
    /// Creates user input received
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="timestampUtc">The timestamp utc</param>
    /// <returns>The resulting desktop session event</returns>
    DesktopSessionEvent CreateUserInputReceived(string sessionId, string toolName, string workingDirectory, string gitBranch, DateTime timestampUtc);

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
    DesktopSessionEvent CreateTurnCompleted(
        string sessionId,
        string message,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName,
        string status);

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
    DesktopSessionEvent CreateTurnCancelled(
        string sessionId,
        string message,
        string workingDirectory,
        string gitBranch,
        string commandName,
        string toolName);
}
