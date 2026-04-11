using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

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
    /// Gets or sets the environment summary
    /// </summary>
    public string EnvironmentSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the session guidance summary
    /// </summary>
    public string SessionGuidanceSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the durable memory summary
    /// </summary>
    public string DurableMemorySummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the session memory summary
    /// </summary>
    public string SessionMemorySummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the user instruction summary
    /// </summary>
    public string UserInstructionSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the workspace instruction summary
    /// </summary>
    public string WorkspaceInstructionSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the mcp server summary
    /// </summary>
    public string McpServerSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the mcp prompt registry summary
    /// </summary>
    public string McpPromptRegistrySummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the scratchpad summary
    /// </summary>
    public string ScratchpadSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the language summary
    /// </summary>
    public string LanguageSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the output style summary
    /// </summary>
    public string OutputStyleSummary { get; init; } = string.Empty;

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
