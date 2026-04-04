namespace QwenCode.App.Models;

public sealed class PendingQuestionAnswerMessageResponse
{
    public string CorrelationId { get; init; } = string.Empty;

    public required DesktopSessionDetail Detail { get; init; }

    public required DesktopSessionEntry PendingQuestion { get; init; }

    public string WorkingDirectory { get; init; } = string.Empty;

    public string GitBranch { get; init; } = string.Empty;

    public IReadOnlyList<DesktopQuestionPrompt> Questions { get; init; } = [];

    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
