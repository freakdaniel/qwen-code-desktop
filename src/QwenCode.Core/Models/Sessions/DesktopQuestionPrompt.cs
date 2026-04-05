namespace QwenCode.App.Models;

/// <summary>
/// Represents the Desktop Question Prompt
/// </summary>
public sealed class DesktopQuestionPrompt
{
    /// <summary>
    /// Gets or sets the header
    /// </summary>
    public string Header { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the question
    /// </summary>
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the multi select
    /// </summary>
    public bool MultiSelect { get; init; }

    /// <summary>
    /// Gets or sets the options
    /// </summary>
    public IReadOnlyList<DesktopQuestionOption> Options { get; init; } = [];
}
