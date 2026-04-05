using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Assistant Prompt Context
/// </summary>
public sealed class AssistantPromptContext
{
    /// <summary>
    /// Gets or sets the messages
    /// </summary>
    public required IReadOnlyList<AssistantConversationMessage> Messages { get; init; }

    /// <summary>
    /// Gets or sets the context files
    /// </summary>
    public required IReadOnlyList<string> ContextFiles { get; init; }

    /// <summary>
    /// Gets or sets the history highlights
    /// </summary>
    public required IReadOnlyList<string> HistoryHighlights { get; init; }

    /// <summary>
    /// Gets or sets the project summary
    /// </summary>
    public ProjectSummarySnapshot? ProjectSummary { get; init; }

    /// <summary>
    /// Gets or sets the session summary
    /// </summary>
    public string SessionSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the was budget trimmed
    /// </summary>
    public bool WasBudgetTrimmed { get; init; }

    /// <summary>
    /// Gets or sets the input token limit
    /// </summary>
    public int InputTokenLimit { get; init; }

    /// <summary>
    /// Gets or sets the approximate input character budget
    /// </summary>
    public int ApproximateInputCharacterBudget { get; init; }

    /// <summary>
    /// Gets or sets the trimmed transcript message count
    /// </summary>
    public int TrimmedTranscriptMessageCount { get; init; }

    /// <summary>
    /// Gets or sets the trimmed context file count
    /// </summary>
    public int TrimmedContextFileCount { get; init; }
}
