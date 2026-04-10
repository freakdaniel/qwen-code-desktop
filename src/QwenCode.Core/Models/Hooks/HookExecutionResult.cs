namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Hook Execution Result
/// </summary>
public sealed class HookExecutionResult
{
    /// <summary>
    /// Gets or sets the hook
    /// </summary>
    public required CommandHookConfiguration Hook { get; init; }

    /// <summary>
    /// Gets or sets the success
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets or sets the exit code
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets or sets the standard output
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the standard error
    /// </summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the output
    /// </summary>
    public HookOutput? Output { get; init; }

    /// <summary>
    /// Gets or sets the duration
    /// </summary>
    public TimeSpan Duration { get; init; }
}
