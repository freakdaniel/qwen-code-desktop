namespace QwenCode.App.Models;

/// <summary>
/// Represents the Ide Diff Update Result
/// </summary>
public sealed class IdeDiffUpdateResult
{
    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the content
    /// </summary>
    public string Content { get; init; } = string.Empty;
}
