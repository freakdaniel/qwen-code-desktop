namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Ide Open File
/// </summary>
public sealed class IdeOpenFile
{
    /// <summary>
    /// Gets or sets the path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the timestamp
    /// </summary>
    public long Timestamp { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is active
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets or sets the selected text
    /// </summary>
    public string SelectedText { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the cursor
    /// </summary>
    public IdeCursorPosition? Cursor { get; init; }
}
