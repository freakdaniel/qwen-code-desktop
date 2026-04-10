namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Shell Command Request
/// </summary>
public sealed class ShellCommandRequest
{
    /// <summary>
    /// Gets or sets the command
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the timeout
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
