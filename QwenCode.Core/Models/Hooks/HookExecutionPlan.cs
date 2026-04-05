namespace QwenCode.App.Models;

public sealed class HookExecutionPlan
{
    public bool Enabled { get; init; } = true;

    public bool Sequential { get; init; }

    public IReadOnlyList<CommandHookConfiguration> Hooks { get; init; } = [];
}
