namespace QwenCode.App.Runtime;

/// <summary>
/// Defines the contract for Assistant Prompt Assembler
/// </summary>
public interface IAssistantPromptAssembler
{
    /// <summary>
    /// Executes assemble async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="tokenLimits">The token limits</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to assistant prompt context</returns>
    Task<AssistantPromptContext> AssembleAsync(
        AssistantTurnRequest request,
        ResolvedTokenLimits? tokenLimits = null,
        CancellationToken cancellationToken = default);
}
