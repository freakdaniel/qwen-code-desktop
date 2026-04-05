namespace QwenCode.App.Models;

public sealed class IdeOpenFile
{
    public required string Path { get; init; }

    public long Timestamp { get; init; }

    public bool IsActive { get; init; }

    public string SelectedText { get; init; } = string.Empty;

    public IdeCursorPosition? Cursor { get; init; }
}
