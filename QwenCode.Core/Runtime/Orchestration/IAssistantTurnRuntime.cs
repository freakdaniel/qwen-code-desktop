namespace QwenCode.App.Runtime;

public interface IAssistantTurnRuntime
{
    Task<AssistantTurnResponse> GenerateAsync(
        AssistantTurnRequest request,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
