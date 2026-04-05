namespace QwenCode.App.Models;

/// <summary>
/// Represents the Create Git Checkpoint Request
/// </summary>
public sealed class CreateGitCheckpointRequest
{
    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
