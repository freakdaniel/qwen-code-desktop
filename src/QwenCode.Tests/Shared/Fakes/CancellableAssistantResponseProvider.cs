namespace QwenCode.Tests.Shared.Fakes;

internal sealed class CancellableAssistantResponseProvider : IAssistantResponseProvider
{
    public string Name => "cancel-test";

    public async Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        return new AssistantTurnResponse
        {
            Summary = "This response should never complete.",
            ProviderName = Name,
            Model = "cancel-model"
        };
    }
}
