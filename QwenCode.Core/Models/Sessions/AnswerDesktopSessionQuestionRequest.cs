namespace QwenCode.App.Models;

public sealed class AnswerDesktopSessionQuestionRequest
{
    public string SessionId { get; init; } = string.Empty;

    public string EntryId { get; init; } = string.Empty;

    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
