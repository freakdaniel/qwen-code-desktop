using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Defines the contract for Loop Detection Service
/// </summary>
public interface ILoopDetectionService
{
    /// <summary>
    /// Executes reset
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    void Reset(string sessionId);

    /// <summary>
    /// Executes observe tool call
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="toolCall">The tool call</param>
    /// <returns>The resulting loop detection decision</returns>
    LoopDetectionDecision ObserveToolCall(string sessionId, AssistantToolCall toolCall);

    /// <summary>
    /// Executes observe content delta
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="contentDelta">The content delta</param>
    /// <returns>The resulting loop detection decision</returns>
    LoopDetectionDecision ObserveContentDelta(string sessionId, string contentDelta);

    /// <summary>
    /// Executes complete
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    void Complete(string sessionId);
}
