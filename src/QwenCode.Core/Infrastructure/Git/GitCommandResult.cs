namespace QwenCode.Core.Infrastructure;

/// <summary>
/// Represents the Git Command Result
/// </summary>
public sealed class GitCommandResult
{
    /// <summary>
    /// Gets or sets the success
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the exit code
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Gets or sets the standard output
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the standard error
    /// </summary>
    public string StandardError { get; init; } = string.Empty;
}
