using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public interface ILoopDetectionService
{
    void Reset(string sessionId);

    LoopDetectionDecision ObserveToolCall(string sessionId, AssistantToolCall toolCall);

    LoopDetectionDecision ObserveContentDelta(string sessionId, string contentDelta);

    void Complete(string sessionId);
}
