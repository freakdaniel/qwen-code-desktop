using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class AssistantPromptContext
{
    public required IReadOnlyList<AssistantConversationMessage> Messages { get; init; }

    public required IReadOnlyList<string> ContextFiles { get; init; }

    public required IReadOnlyList<string> HistoryHighlights { get; init; }

    public ProjectSummarySnapshot? ProjectSummary { get; init; }

    public string SessionSummary { get; init; } = string.Empty;

    public bool WasBudgetTrimmed { get; init; }

    public int InputTokenLimit { get; init; }

    public int ApproximateInputCharacterBudget { get; init; }

    public int TrimmedTranscriptMessageCount { get; init; }

    public int TrimmedContextFileCount { get; init; }
}
