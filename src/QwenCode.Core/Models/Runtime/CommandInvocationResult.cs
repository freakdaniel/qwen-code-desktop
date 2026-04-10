using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Command Invocation Result
/// </summary>
public sealed class CommandInvocationResult
{
    /// <summary>
    /// Gets or sets the command
    /// </summary>
    public required ResolvedCommand Command { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the output
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether is terminal
    /// </summary>
    public bool IsTerminal { get; init; }
}
