namespace QwenCode.App.Ide;

/// <summary>
/// Represents the Ide Command Result
/// </summary>
public sealed class IdeCommandResult
{
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
    /// Gets the success
    /// </summary>
    public bool Success => ExitCode == 0;
}
