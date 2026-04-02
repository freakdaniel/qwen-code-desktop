using QwenCode.App.Options;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface IAssistantResponseProvider
{
    string Name { get; }

    Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
