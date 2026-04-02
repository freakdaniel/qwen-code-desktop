namespace QwenCode.App.Runtime;

public interface IAssistantPromptAssembler
{
    Task<AssistantPromptContext> AssembleAsync(
        AssistantTurnRequest request,
        CancellationToken cancellationToken = default);
}
