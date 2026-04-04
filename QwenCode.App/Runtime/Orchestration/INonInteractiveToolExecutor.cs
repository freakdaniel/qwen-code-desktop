using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface INonInteractiveToolExecutor
{
    Task<NativeToolExecutionResult> ExecuteAsync(
        AssistantTurnRequest request,
        AssistantToolCall toolCall,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
