namespace QwenCode.App.Models;

/// <summary>
/// Represents the Subagent Validation Result
/// </summary>
public sealed class SubagentValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether is valid
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets or sets the errors
    /// </summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Gets or sets the warnings
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
