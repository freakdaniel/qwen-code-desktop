namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Hook Execution Plan
/// </summary>
public sealed class HookExecutionPlan
{
    /// <summary>
    /// Gets or sets the enabled
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the sequential
    /// </summary>
    public bool Sequential { get; init; }

    /// <summary>
    /// Gets or sets the hooks
    /// </summary>
    public IReadOnlyList<CommandHookConfiguration> Hooks { get; init; } = [];
}
