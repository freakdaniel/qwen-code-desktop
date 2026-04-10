namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Ide Context Snapshot
/// </summary>
public sealed class IdeContextSnapshot
{
    /// <summary>
    /// Gets or sets the open files
    /// </summary>
    public IReadOnlyList<IdeOpenFile> OpenFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether is trusted
    /// </summary>
    public bool? IsTrusted { get; init; }
}
