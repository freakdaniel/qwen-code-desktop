namespace QwenCode.App.Models;

/// <summary>
/// Represents the Answer Desktop Session Question Request
/// </summary>
public sealed class AnswerDesktopSessionQuestionRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the entry id
    /// </summary>
    public string EntryId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the answers
    /// </summary>
    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
