using QwenCode.App.Options;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Defines the contract for Assistant Response Provider
/// </summary>
public interface IAssistantResponseProvider
{
    /// <summary>
    /// Gets the name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Attempts to generate async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="promptContext">The prompt context</param>
    /// <param name="toolHistory">The tool history</param>
    /// <param name="options">The options</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to assistant turn response?</returns>
    Task<AssistantTurnResponse?> TryGenerateAsync(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory,
        NativeAssistantRuntimeOptions options,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
