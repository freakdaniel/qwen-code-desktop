namespace QwenCode.App.Models;

/// <summary>
/// Represents the Shell Command Execution Result
/// </summary>
public sealed class ShellCommandExecutionResult
{
    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the output
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets the exit code
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Gets or sets the timed out
    /// </summary>
    public required bool TimedOut { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether cancelled
    /// </summary>
    public required bool Cancelled { get; init; }
}
