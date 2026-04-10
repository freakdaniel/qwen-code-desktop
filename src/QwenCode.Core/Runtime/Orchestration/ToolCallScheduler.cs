using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Tool Call Scheduler
/// </summary>
/// <param name="toolExecutor">The tool executor</param>
/// <param name="loopDetectionService">The loop detection service</param>
public sealed class ToolCallScheduler(
    INonInteractiveToolExecutor toolExecutor,
    ILoopDetectionService loopDetectionService) : IToolCallScheduler
{
    /// <summary>
    /// Executes schedule async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="providerName">The provider name</param>
    /// <param name="model">The model</param>
    /// <param name="toolCalls">The tool calls</param>
    /// <param name="toolHistory">The tool history</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to tool scheduling result</returns>
    public async Task<ToolSchedulingResult> ScheduleAsync(
        AssistantTurnRequest request,
        string providerName,
        string model,
        IReadOnlyList<AssistantToolCall> toolCalls,
        List<AssistantToolCallResult> toolHistory,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        var toolCallGroupId = $"tool-group-{Guid.NewGuid():N}";

        foreach (var toolCall in toolCalls)
        {
            var loopDecision = loopDetectionService.ObserveToolCall(request.SessionId, toolCall);
            if (loopDecision.IsDetected)
            {
                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "loop-detected",
                    ProviderName = providerName,
                    ToolName = toolCall.ToolName,
                    ToolCallId = toolCall.Id,
                    ToolCallGroupId = toolCallGroupId,
                    ToolArgumentsJson = NormalizeToolArguments(toolCall.ArgumentsJson),
                    Status = "blocked",
                    Message = loopDecision.Reason
                });

                return new ToolSchedulingResult
                {
                    ContinueTurnLoop = false,
                    TerminalSummary = $"Stopped because the same tool call for '{toolCall.ToolName}' kept repeating.",
                    TerminalStopReason = "tool-loop-detected"
                };
            }

            if (request.AllowedToolNames.Count > 0 &&
                !request.AllowedToolNames.Contains(toolCall.ToolName, StringComparer.OrdinalIgnoreCase))
            {
                var deniedExecution = CreateDisallowedToolExecutionResult(toolCall.ToolName, request.RuntimeProfile.ProjectRoot);
                toolHistory.Add(new AssistantToolCallResult
                {
                    ToolCall = toolCall,
                    Execution = deniedExecution
                });

                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "tool-blocked",
                    ProviderName = providerName,
                    ToolName = toolCall.ToolName,
                    ToolCallId = toolCall.Id,
                    ToolCallGroupId = toolCallGroupId,
                    ToolArgumentsJson = NormalizeToolArguments(toolCall.ArgumentsJson),
                    Status = deniedExecution.Status,
                    Message = BuildToolMessage(deniedExecution),
                    ToolOutput = string.IsNullOrWhiteSpace(deniedExecution.Output) ? deniedExecution.ErrorMessage : deniedExecution.Output,
                    ApprovalState = deniedExecution.ApprovalState,
                    ChangedFiles = deniedExecution.ChangedFiles,
                    Questions = deniedExecution.Questions,
                    Answers = deniedExecution.Answers
                });
                continue;
            }

            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "tool-requested",
                ProviderName = providerName,
                ToolName = toolCall.ToolName,
                ToolCallId = toolCall.Id,
                ToolCallGroupId = toolCallGroupId,
                ToolArgumentsJson = NormalizeToolArguments(toolCall.ArgumentsJson),
                Status = "requested",
                Message = $"Starting tool '{toolCall.ToolName}'."
            });

            var execution = await toolExecutor.ExecuteAsync(
                request,
                toolCall,
                toolEvent => eventSink?.Invoke(CloneNestedToolEvent(toolEvent, providerName, toolCall, toolCallGroupId)),
                cancellationToken);

            var toolResult = new AssistantToolCallResult
            {
                ToolCall = toolCall,
                Execution = execution
            };
            toolHistory.Add(toolResult);

            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = MapToolStage(execution.Status),
                ProviderName = providerName,
                ToolName = execution.ToolName,
                ToolCallId = toolCall.Id,
                ToolCallGroupId = toolCallGroupId,
                ToolArgumentsJson = NormalizeToolArguments(toolCall.ArgumentsJson),
                Status = execution.Status,
                Message = BuildToolMessage(execution),
                ToolOutput = string.IsNullOrWhiteSpace(execution.Output) ? execution.ErrorMessage : execution.Output,
                ApprovalState = execution.ApprovalState,
                ChangedFiles = execution.ChangedFiles,
                Questions = execution.Questions,
                Answers = execution.Answers
            });

            if (ShouldPauseTurnLoop(execution.Status))
            {
                return new ToolSchedulingResult
                {
                    ContinueTurnLoop = false,
                    TerminalSummary = BuildTerminalSummary(execution),
                    TerminalStopReason = AssistantExecutionDiagnostics.ResolveStopReasonFromStatus(execution.Status)
                };
            }
        }

        return new ToolSchedulingResult
        {
            ContinueTurnLoop = true
        };
    }

    private static bool ShouldPauseTurnLoop(string status) =>
        status switch
        {
            "approval-required" => true,
            "input-required" => true,
            "cancelled" => true,
            _ => false
        };

    private static string BuildTerminalSummary(NativeToolExecutionResult execution) =>
        execution.Status switch
        {
            "approval-required" => $"Tool '{execution.ToolName}' is waiting for approval before the turn can continue.",
            "input-required" => $"Tool '{execution.ToolName}' is waiting for user input before the turn can continue.",
            "blocked" => $"Tool '{execution.ToolName}' was blocked by approval policy.",
            "error" => $"Tool '{execution.ToolName}' failed: {execution.ErrorMessage}",
            _ => $"Tool '{execution.ToolName}' changed state to '{execution.Status}'."
        };

    private static string MapToolStage(string status) =>
        status switch
        {
            "approval-required" => "tool-approval-required",
            "input-required" => "user-input-required",
            "blocked" => "tool-blocked",
            "error" => "tool-failed",
            _ => "tool-completed"
        };

    private static string BuildToolMessage(NativeToolExecutionResult execution) =>
        execution.Status switch
        {
            "approval-required" => $"Tool '{execution.ToolName}' is waiting for approval.",
            "input-required" => $"Tool '{execution.ToolName}' is waiting for user input.",
            "blocked" => $"Tool '{execution.ToolName}' was blocked by approval policy.",
            "error" => $"Tool '{execution.ToolName}' failed: {execution.ErrorMessage}",
            _ => $"Tool '{execution.ToolName}' completed."
        };

    private static NativeToolExecutionResult CreateDisallowedToolExecutionResult(string toolName, string workingDirectory) =>
        new()
        {
            ToolName = toolName,
            Status = "blocked",
            ApprovalState = "deny",
            WorkingDirectory = workingDirectory,
            ErrorMessage = $"Tool '{toolName}' is not available in this delegated runtime.",
            ChangedFiles = []
        };

    private static AssistantRuntimeEvent CloneNestedToolEvent(
        AssistantRuntimeEvent runtimeEvent,
        string providerName,
        AssistantToolCall toolCall,
        string toolCallGroupId) =>
        new()
        {
            Stage = runtimeEvent.Stage,
            Message = runtimeEvent.Message,
            ProviderName = string.IsNullOrWhiteSpace(runtimeEvent.ProviderName) ? providerName : runtimeEvent.ProviderName,
            ToolName = string.IsNullOrWhiteSpace(runtimeEvent.ToolName) ? toolCall.ToolName : runtimeEvent.ToolName,
            ToolCallId = string.IsNullOrWhiteSpace(runtimeEvent.ToolCallId) ? toolCall.Id : runtimeEvent.ToolCallId,
            ToolCallGroupId = string.IsNullOrWhiteSpace(runtimeEvent.ToolCallGroupId) ? toolCallGroupId : runtimeEvent.ToolCallGroupId,
            ToolArgumentsJson = NormalizeToolArguments(
                string.IsNullOrWhiteSpace(runtimeEvent.ToolArgumentsJson) || runtimeEvent.ToolArgumentsJson == "{}"
                    ? toolCall.ArgumentsJson
                    : runtimeEvent.ToolArgumentsJson),
            Status = runtimeEvent.Status,
            ContentDelta = runtimeEvent.ContentDelta,
            ContentSnapshot = runtimeEvent.ContentSnapshot,
            AgentName = runtimeEvent.AgentName,
            ToolOutput = runtimeEvent.ToolOutput,
            ApprovalState = runtimeEvent.ApprovalState,
            ChangedFiles = runtimeEvent.ChangedFiles,
            Questions = runtimeEvent.Questions,
            Answers = runtimeEvent.Answers
        };

    private static string NormalizeToolArguments(string argumentsJson) =>
        string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson;
}
