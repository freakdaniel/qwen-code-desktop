namespace QwenCode.App.Models;

public sealed class HookLifecycleResult
{
    public HookOutput AggregateOutput { get; init; } = new()
    {
        Decision = "allow"
    };

    public bool IsBlocked { get; init; }

    public string BlockReason { get; init; } = string.Empty;

    public IReadOnlyList<HookExecutionResult> Executions { get; init; } = [];
}
