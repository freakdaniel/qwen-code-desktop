namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Fallback Assistant Response Provider
/// </summary>
public sealed class FallbackAssistantResponseProvider : IAssistantResponseProvider
{
    /// <summary>
    /// Gets the name
    /// </summary>
    public string Name => "fallback";

    /// <summary>
    /// Attempts to generate async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="promptContext">The prompt context</param>
    /// <param name="toolHistory">The tool history</param>
    /// <param name="options">The options</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to assistant turn response?</returns>
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
                "completed" => $"Tool '{lastTool.Execution.ToolName}' completed and the turn finished.",
                "approval-required" => $"Tool '{lastTool.Execution.ToolName}' is waiting for approval before the turn can continue.",
                "input-required" => $"Tool '{lastTool.Execution.ToolName}' is waiting for user input before the turn can continue.",
                "blocked" => $"Tool '{lastTool.Execution.ToolName}' was blocked by approval policy.",
                "error" => $"Tool '{lastTool.Execution.ToolName}' failed: {lastTool.Execution.ErrorMessage}",
                _ => $"Tool '{lastTool.Execution.ToolName}' updated the turn."
            };
        }

        if (request.IsApprovalResolution)
        {
            if (string.Equals(request.ToolExecution.ToolName, "ask_user_question", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.ToolExecution.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return "Captured the user's answers and continued.";
            }

            return request.ToolExecution.Status switch
            {
                "completed" => $"Approved tool '{request.ToolExecution.ToolName}' and executed it.",
                "blocked" => $"Approved tool '{request.ToolExecution.ToolName}', but policy still blocked the execution.",
                "error" => $"Approved tool '{request.ToolExecution.ToolName}', but execution failed: {request.ToolExecution.ErrorMessage}",
                _ => $"Approved tool '{request.ToolExecution.ToolName}' for execution."
            };
        }

        if (request.CommandInvocation is { IsTerminal: true } commandInvocation)
        {
            return commandInvocation.Status switch
            {
                "completed" => $"Built-in command '/{commandInvocation.Command.Name}' completed.",
                "error" => $"Built-in command '/{commandInvocation.Command.Name}' failed: {commandInvocation.ErrorMessage}",
                _ => $"Built-in command '/{commandInvocation.Command.Name}' updated the turn."
            };
        }

        if (request.ResolvedCommand is not null && request.ToolExecution.Status == "not-requested")
        {
            return $"Slash command '/{request.ResolvedCommand.Name}' resolved.";
        }

        if (request.ResolvedCommand is not null)
        {
            return request.ToolExecution.Status switch
            {
                "completed" => $"Slash command '/{request.ResolvedCommand.Name}' resolved and tool '{request.ToolExecution.ToolName}' completed.",
                "approval-required" => $"Slash command '/{request.ResolvedCommand.Name}' resolved and tool '{request.ToolExecution.ToolName}' is waiting for approval.",
                "blocked" => $"Slash command '/{request.ResolvedCommand.Name}' resolved, but tool '{request.ToolExecution.ToolName}' was blocked by approval policy.",
                "error" => $"Slash command '/{request.ResolvedCommand.Name}' resolved, but tool '{request.ToolExecution.ToolName}' failed: {request.ToolExecution.ErrorMessage}",
                _ => $"Slash command '/{request.ResolvedCommand.Name}' updated the turn."
            };
        }

        if (request.ToolExecution.Status == "not-requested")
        {
            var prompt = request.Prompt.Trim();
            return string.IsNullOrWhiteSpace(prompt)
                ? $"Recorded the turn with {promptContext.Messages.Count} context message(s)."
                : $"Recorded the turn for: {TrimForSummary(prompt)}";
        }

        return request.ToolExecution.Status switch
        {
            "completed" => $"Tool '{request.ToolExecution.ToolName}' completed.",
            "approval-required" => $"Tool '{request.ToolExecution.ToolName}' is waiting for approval before execution.",
            "input-required" => $"Tool '{request.ToolExecution.ToolName}' is waiting for user answers before execution can continue.",
            "blocked" => $"Tool '{request.ToolExecution.ToolName}' was blocked by approval policy.",
            "error" => $"Tool '{request.ToolExecution.ToolName}' failed: {request.ToolExecution.ErrorMessage}",
            _ => $"Tool '{request.ToolExecution.ToolName}' updated the turn."
        };
    }

    private static string TrimForSummary(string prompt) =>
        prompt.Length <= 96 ? prompt : $"{prompt[..96]}...";
}
