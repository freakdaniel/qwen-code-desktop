namespace QwenCode.App.Models;

/// <summary>
/// Represents the result of selecting a project directory from the desktop shell.
/// </summary>
public sealed class SelectProjectDirectoryResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the picker was cancelled.
    /// </summary>
    public required bool Cancelled { get; init; }

    /// <summary>
    /// Gets or sets the selected path.
    /// </summary>
    public string SelectedPath { get; init; } = string.Empty;
}
