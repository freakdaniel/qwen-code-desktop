using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Runtime;

public sealed class FallbackAssistantResponseProvider : IAssistantResponseProvider
{
    public string Name => "fallback";

    public Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        var summary = BuildSummary(request, promptContext, toolHistory);
        return Task.FromResult<AssistantTurnResponse?>(
            new AssistantTurnResponse
            {
                Summary = summary,
                ProviderName = Name,
                Model = string.IsNullOrWhiteSpace(request.ModelOverride) ? options.Model : request.ModelOverride,
                ToolExecutions = toolHistory.ToArray()
            });
    }

    private static string BuildSummary(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory)
    {
        if (toolHistory.Count > 0)
        {
            var lastTool = toolHistory[^1];
            return lastTool.Execution.Status switch
            {
                "completed" => $"Assistant runtime used native tool '{lastTool.Execution.ToolName}' and completed the turn inside the .NET host.",
                "approval-required" => $"Assistant runtime requested native tool '{lastTool.Execution.ToolName}', but it is waiting for approval before the turn can continue.",
                "input-required" => $"Assistant runtime requested native tool '{lastTool.Execution.ToolName}', but it is waiting for user answers before the turn can continue.",
                "blocked" => $"Assistant runtime requested native tool '{lastTool.Execution.ToolName}', but qwen-compatible approval policy blocked it.",
                "error" => $"Assistant runtime requested native tool '{lastTool.Execution.ToolName}', but execution failed: {lastTool.Execution.ErrorMessage}",
                _ => $"Assistant runtime updated the session after using native tool '{lastTool.Execution.ToolName}'."
            };
        }

        if (request.IsApprovalResolution)
        {
            if (string.Equals(request.ToolExecution.ToolName, "ask_user_question", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.ToolExecution.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return "Captured user answers for ask_user_question and continued the native desktop session.";
            }

            return request.ToolExecution.Status switch
            {
                "completed" => $"Approved native tool '{request.ToolExecution.ToolName}' and executed it inside the .NET host.",
                "blocked" => $"Approved native tool '{request.ToolExecution.ToolName}', but the execution is blocked by qwen-compatible approval policy.",
                "error" => $"Approved native tool '{request.ToolExecution.ToolName}', but execution failed: {request.ToolExecution.ErrorMessage}",
                _ => $"Approved native tool '{request.ToolExecution.ToolName}' for execution."
            };
        }

        if (request.CommandInvocation is { IsTerminal: true } commandInvocation)
        {
            return commandInvocation.Status switch
            {
                "completed" => $"Built-in command '/{commandInvocation.Command.Name}' completed in the native .NET runtime.",
                "error" => $"Built-in command '/{commandInvocation.Command.Name}' failed: {commandInvocation.ErrorMessage}",
                _ => $"Built-in command '/{commandInvocation.Command.Name}' updated the desktop session."
            };
        }

        if (request.ResolvedCommand is not null && request.ToolExecution.Status == "not-requested")
        {
            return $"Slash command '/{request.ResolvedCommand.Name}' resolved by the native .NET runtime.";
        }

        if (request.ResolvedCommand is not null)
        {
            return request.ToolExecution.Status switch
            {
                "completed" => $"Slash command '/{request.ResolvedCommand.Name}' resolved and native tool '{request.ToolExecution.ToolName}' completed inside the .NET host.",
                "approval-required" => $"Slash command '/{request.ResolvedCommand.Name}' resolved and native tool '{request.ToolExecution.ToolName}' is waiting for approval.",
                "blocked" => $"Slash command '/{request.ResolvedCommand.Name}' resolved, but native tool '{request.ToolExecution.ToolName}' was blocked by qwen-compatible approval policy.",
                "error" => $"Slash command '/{request.ResolvedCommand.Name}' resolved, but native tool '{request.ToolExecution.ToolName}' failed: {request.ToolExecution.ErrorMessage}",
                _ => $"Slash command '/{request.ResolvedCommand.Name}' updated the desktop session."
            };
        }

        if (request.ToolExecution.Status == "not-requested")
        {
            var prompt = request.Prompt.Trim();
            return string.IsNullOrWhiteSpace(prompt)
                ? $"Turn recorded in the native desktop session host with {promptContext.Messages.Count} context messages."
                : $"Turn recorded in the native desktop session host for: {TrimForSummary(prompt)}";
        }

        return request.ToolExecution.Status switch
        {
            "completed" => $"Native tool '{request.ToolExecution.ToolName}' completed inside the .NET host.",
            "approval-required" => $"Native tool '{request.ToolExecution.ToolName}' is waiting for approval before execution.",
            "input-required" => $"Native tool '{request.ToolExecution.ToolName}' is waiting for user answers before execution can continue.",
            "blocked" => $"Native tool '{request.ToolExecution.ToolName}' was blocked by qwen-compatible approval policy.",
            "error" => $"Native tool '{request.ToolExecution.ToolName}' failed: {request.ToolExecution.ErrorMessage}",
            _ => $"Native tool '{request.ToolExecution.ToolName}' updated the desktop session."
        };
    }

    private static string TrimForSummary(string prompt) =>
        prompt.Length <= 96 ? prompt : $"{prompt[..96]}...";
}
