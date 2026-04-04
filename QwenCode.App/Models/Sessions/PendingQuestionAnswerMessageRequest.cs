namespace QwenCode.App.Models;

public sealed class PendingQuestionAnswerMessageRequest
{
    public string CorrelationId { get; init; } = string.Empty;

    public required WorkspacePaths Paths { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public string EntryId { get; init; } = string.Empty;

    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
