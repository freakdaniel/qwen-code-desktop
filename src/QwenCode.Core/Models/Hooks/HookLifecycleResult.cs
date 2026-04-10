namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Hook Lifecycle Result
/// </summary>
public sealed class HookLifecycleResult
{
    /// <summary>
    /// Gets or sets the aggregate output
    /// </summary>
    public HookOutput AggregateOutput { get; init; } = new()
    {
        Decision = "allow"
    };

    /// <summary>
    /// Gets or sets a value indicating whether is blocked
    /// </summary>
    public bool IsBlocked { get; init; }

    /// <summary>
    /// Gets or sets the block reason
    /// </summary>
    public string BlockReason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the executions
    /// </summary>
    public IReadOnlyList<HookExecutionResult> Executions { get; init; } = [];
}
