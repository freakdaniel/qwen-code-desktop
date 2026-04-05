namespace QwenCode.Tests.Shared.Fakes;

internal sealed class StaticAssistantResponseProvider(
    string providerName,
    Func<AssistantTurnRequest, AssistantPromptContext, AssistantTurnResponse?> responder) : IAssistantResponseProvider
{
    public string Name => providerName;

    public Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(responder(request, promptContext));
}
