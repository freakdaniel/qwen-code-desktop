namespace QwenCode.Core.Runtime;

/// <summary>
/// Defines the contract for Tool Call Scheduler
/// </summary>
public interface IToolCallScheduler
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
    Task<ToolSchedulingResult> ScheduleAsync(
        AssistantTurnRequest request,
        string providerName,
        string model,
        IReadOnlyList<AssistantToolCall> toolCalls,
        List<AssistantToolCallResult> toolHistory,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
