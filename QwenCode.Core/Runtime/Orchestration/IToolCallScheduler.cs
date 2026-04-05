namespace QwenCode.App.Runtime;

public interface IToolCallScheduler
{
    Task<ToolSchedulingResult> ScheduleAsync(
        AssistantTurnRequest request,
        string providerName,
        string model,
        IReadOnlyList<AssistantToolCall> toolCalls,
        List<AssistantToolCallResult> toolHistory,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
