namespace QwenCode.App.Models;

/// <summary>
/// Represents the Ide Cursor Position
/// </summary>
public sealed class IdeCursorPosition
{
    /// <summary>
    /// Gets or sets the line
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Gets or sets the character
    /// </summary>
    public int Character { get; init; }
}
