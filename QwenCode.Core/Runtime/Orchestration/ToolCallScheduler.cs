using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class ToolCallScheduler(
    INonInteractiveToolExecutor toolExecutor,
    ILoopDetectionService loopDetectionService) : IToolCallScheduler
{
    public async Task<ToolSchedulingResult> ScheduleAsync(
        AssistantTurnRequest request,
        string providerName,
        string model,
        IReadOnlyList<AssistantToolCall> toolCalls,
        List<AssistantToolCallResult> toolHistory,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
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
                    Status = "blocked",
                    Message = loopDecision.Reason
                });

                return new ToolSchedulingResult
                {
                    ContinueTurnLoop = false,
                    TerminalSummary = $"Assistant runtime stopped because loop detection found repeated tool calls for '{toolCall.ToolName}'.",
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
                    Status = deniedExecution.Status,
                    Message = BuildToolMessage(deniedExecution)
                });

                return new ToolSchedulingResult
                {
                    ContinueTurnLoop = false,
                    TerminalSummary = $"Assistant runtime requested native tool '{toolCall.ToolName}', but this subagent runtime is not allowed to execute it.",
                    TerminalStopReason = "tool-blocked"
                };
            }

            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "tool-requested",
                ProviderName = providerName,
                ToolName = toolCall.ToolName,
                Status = "requested",
                Message = $"Assistant runtime requested native tool '{toolCall.ToolName}'."
            });

            var execution = await toolExecutor.ExecuteAsync(
                request,
                toolCall,
                toolEvent => eventSink?.Invoke(CloneNestedToolEvent(toolEvent, providerName)),
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
                Status = execution.Status,
                Message = BuildToolMessage(execution)
            });

            if (!string.Equals(execution.Status, "completed", StringComparison.OrdinalIgnoreCase))
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

    private static string BuildTerminalSummary(NativeToolExecutionResult execution) =>
        execution.Status switch
        {
            "approval-required" => $"Assistant runtime requested native tool '{execution.ToolName}', but it is waiting for approval before the turn can continue.",
            "input-required" => $"Assistant runtime requested native tool '{execution.ToolName}', but it is waiting for user answers before the turn can continue.",
            "blocked" => $"Assistant runtime requested native tool '{execution.ToolName}', but qwen-compatible approval policy blocked it.",
            "error" => $"Assistant runtime requested native tool '{execution.ToolName}', but execution failed: {execution.ErrorMessage}",
            _ => $"Assistant runtime stopped after native tool '{execution.ToolName}' changed state to '{execution.Status}'."
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
            "approval-required" => $"Assistant runtime requested native tool '{execution.ToolName}', and it is waiting for approval.",
            "input-required" => $"Assistant runtime requested native tool '{execution.ToolName}', and it is waiting for user answers.",
            "blocked" => $"Assistant runtime requested native tool '{execution.ToolName}', but approval policy blocked it.",
            "error" => $"Assistant runtime requested native tool '{execution.ToolName}', but execution failed: {execution.ErrorMessage}",
            _ => $"Assistant runtime completed native tool '{execution.ToolName}'."
        };

    private static NativeToolExecutionResult CreateDisallowedToolExecutionResult(string toolName, string workingDirectory) =>
        new()
        {
            ToolName = toolName,
            Status = "blocked",
            ApprovalState = "deny",
            WorkingDirectory = workingDirectory,
            ErrorMessage = $"Tool '{toolName}' is not available to this subagent runtime.",
            ChangedFiles = []
        };

    private static AssistantRuntimeEvent CloneNestedToolEvent(AssistantRuntimeEvent runtimeEvent, string providerName) =>
        new()
        {
            Stage = runtimeEvent.Stage,
            Message = runtimeEvent.Message,
            ProviderName = string.IsNullOrWhiteSpace(runtimeEvent.ProviderName) ? providerName : runtimeEvent.ProviderName,
            ToolName = runtimeEvent.ToolName,
            Status = runtimeEvent.Status,
            ContentDelta = runtimeEvent.ContentDelta,
            ContentSnapshot = runtimeEvent.ContentSnapshot,
            AgentName = runtimeEvent.AgentName
        };
}
