namespace QwenCode.App.Models;

/// <summary>
/// Represents the Ide Install Result
/// </summary>
public sealed class IdeInstallResult
{
    /// <summary>
    /// Gets or sets the success
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the command path
    /// </summary>
    public string CommandPath { get; init; } = string.Empty;
}
