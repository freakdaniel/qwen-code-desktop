namespace QwenCode.Core.Models;

/// <summary>
/// Represents the User Prompt Hook Result
/// </summary>
public sealed class UserPromptHookResult
{
    /// <summary>
    /// Gets or sets the effective prompt
    /// </summary>
    public required string EffectivePrompt { get; init; }

    /// <summary>
    /// Gets or sets the additional context
    /// </summary>
    public string AdditionalContext { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the system message
    /// </summary>
    public string SystemMessage { get; init; } = string.Empty;

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
