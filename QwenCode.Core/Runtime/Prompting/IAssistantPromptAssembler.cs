namespace QwenCode.App.Runtime;

public interface IAssistantPromptAssembler
{
    Task<AssistantPromptContext> AssembleAsync(
        AssistantTurnRequest request,
        ResolvedTokenLimits? tokenLimits = null,
        CancellationToken cancellationToken = default);
}
