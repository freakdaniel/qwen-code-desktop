namespace QwenCode.Core.Runtime;

/// <summary>
/// Defines the contract for Assistant Turn Runtime
/// </summary>
public interface IAssistantTurnRuntime
{
    /// <summary>
    /// Generates async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to assistant turn response</returns>
    Task<AssistantTurnResponse> GenerateAsync(
        AssistantTurnRequest request,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
