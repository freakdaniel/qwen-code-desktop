namespace QwenCode.App.Models;

public sealed class HookOutput
{
    public bool? Continue { get; init; }

    public string StopReason { get; init; } = string.Empty;

    public string Decision { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string SystemMessage { get; init; } = string.Empty;

    public string AdditionalContext { get; init; } = string.Empty;

    public string ModifiedPrompt { get; init; } = string.Empty;
}
