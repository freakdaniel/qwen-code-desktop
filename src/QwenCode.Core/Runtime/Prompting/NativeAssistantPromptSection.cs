namespace QwenCode.App.Runtime;

internal sealed record NativeAssistantPromptSection(
    string Name,
    Func<NativeAssistantPromptCompositionContext, string?> Compute,
    int Order = 0,
    bool IsDynamic = false,
    Func<NativeAssistantPromptCompositionContext, bool>? Applies = null)
{
    public bool AppliesTo(NativeAssistantPromptCompositionContext context) => Applies?.Invoke(context) ?? true;
}
