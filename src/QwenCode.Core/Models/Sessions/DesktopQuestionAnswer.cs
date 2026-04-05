namespace QwenCode.App.Models;

/// <summary>
/// Represents the Desktop Question Answer
/// </summary>
public sealed class DesktopQuestionAnswer
{
    /// <summary>
    /// Gets or sets the question index
    /// </summary>
    public int QuestionIndex { get; init; }

    /// <summary>
    /// Gets or sets the value
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
