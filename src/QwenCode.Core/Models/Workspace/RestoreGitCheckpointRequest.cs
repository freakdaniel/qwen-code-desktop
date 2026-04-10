namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Restore Git Checkpoint Request
/// </summary>
public sealed class RestoreGitCheckpointRequest
{
    /// <summary>
    /// Gets or sets the commit hash
    /// </summary>
    public string CommitHash { get; init; } = string.Empty;
}
