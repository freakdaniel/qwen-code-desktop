using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Defines the contract for Non Interactive Tool Executor
/// </summary>
public interface INonInteractiveToolExecutor
{
    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="toolCall">The tool call</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    Task<NativeToolExecutionResult> ExecuteAsync(
        AssistantTurnRequest request,
        AssistantToolCall toolCall,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
