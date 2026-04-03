namespace QwenCode.App.Models;

public sealed class UserPromptHookResult
{
    public required string EffectivePrompt { get; init; }

    public string AdditionalContext { get; init; } = string.Empty;

    public string SystemMessage { get; init; } = string.Empty;

    public bool IsBlocked { get; init; }

    public string BlockReason { get; init; } = string.Empty;

    public IReadOnlyList<HookExecutionResult> Executions { get; init; } = [];
}
