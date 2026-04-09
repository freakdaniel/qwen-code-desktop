namespace QwenCode.App.Runtime;

/// <summary>
/// Provides reusable utility prompts for specialized assistant subtasks such as
/// long-session maintenance, memory hygiene, and title generation.
/// </summary>
public static class NativeAssistantUtilityPromptCatalog
{
    /// <summary>
    /// Builds the prompt section that teaches the assistant how to keep state
    /// coherent during long or trimmed sessions.
    /// </summary>
    /// <param name="wasBudgetTrimmed">Whether prompt budgeting trimmed context for the current turn.</param>
    /// <param name="trimmedTranscriptCount">The number of transcript messages trimmed away.</param>
    /// <param name="trimmedContextFileCount">The number of context files trimmed away.</param>
    /// <returns>The long-session maintenance prompt section.</returns>
    public static string BuildLongSessionMaintenancePrompt(bool wasBudgetTrimmed, int trimmedTranscriptCount, int trimmedContextFileCount)
    {
        var lines = new List<string>
        {
            "# Long-Session Maintenance",
            "- As the session grows, keep a compact working memory of the current goal, the active plan, the key findings, and the concrete blocker or next action.",
            "- Prefer short synthesized state over repeating long transcript history.",
            "- When a conclusion, command, path, URL, or decision will matter later, restate it in a compact form before the context moves on."
        };

        if (wasBudgetTrimmed)
        {
            lines.Add($"- This turn already trimmed {trimmedTranscriptCount} transcript message(s) and {trimmedContextFileCount} context file(s), so rely on compact carry-forward summaries rather than assuming raw history is still present.");
        }

        lines.Add("- If the task changes direction, update the plan or task tracker so the new working state survives future compression.");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Builds the prompt section that constrains how durable memory should be used.
    /// </summary>
    /// <param name="canSaveMemory">Whether the current turn exposes the save_memory tool.</param>
    /// <returns>The memory hygiene prompt section.</returns>
    public static string BuildMemoryHygienePrompt(bool canSaveMemory) =>
        $$"""
# Memory Hygiene
- Treat durable memory as a high-signal store, not as a dump of everything the model just saw.
- Save only stable preferences, project conventions, reusable facts, or constraints that are likely to matter again in future turns.
- Do not treat temporary blockers, one-off command output, partial hypotheses, or turn-local plans as durable memory.
{{(canSaveMemory ? "- When a fact is both durable and likely to matter later, use `save_memory` deliberately instead of hoping it remains in raw context." : "- If memory-writing tools are unavailable, still keep durable conventions explicit in your plan or response when they matter.")}}
""";

    /// <summary>
    /// Builds the specialized system prompt used for chat title generation.
    /// </summary>
    /// <param name="locale">The UI locale code.</param>
    /// <param name="language">The human-readable language name.</param>
    /// <returns>The title generation prompt.</returns>
    public static string BuildSessionTitlePrompt(string locale, string language) =>
        $$"""
You generate short titles for coding chat sessions.

# Title Requirements
- Reply in {{language}}.
- The application UI locale is {{locale}}.
- Return exactly one short title, ideally 3-6 words and never more than 7 words.
- Capture the user's intent or task, not a full sentence.
- Prefer the same language as the UI locale unless the user's message clearly requires another language.
- Do not use quotation marks.
- Do not add markdown, numbering, labels, or explanations.
- Do not end with punctuation.
- Do not mention that this is a chat, session, conversation, or request unless that is the core topic.
- Favor concrete task wording over generic titles like "Coding Help" or "Research Task".

Return only the title.
""";

    /// <summary>
    /// Builds the specialized prompt used to summarize older transcript history
    /// into a compact carry-forward checkpoint.
    /// </summary>
    /// <param name="locale">The UI locale code.</param>
    /// <param name="language">The human-readable language name.</param>
    /// <returns>The session compaction prompt.</returns>
    public static string BuildSessionCompactionPrompt(string locale, string language) =>
        $$"""
You compress older coding-session history into a compact carry-forward checkpoint.

# Output Requirements
- Reply in {{language}}.
- The application UI locale is {{locale}}.
- Return only bullet points.
- Return between 4 and 8 bullets.
- Each bullet must be short, concrete, and reusable in later turns.

# What To Preserve
- The current goal or active workstream if it is clear.
- Important findings, decisions, constraints, commands, file paths, URLs, or blockers that later turns may still need.
- Any unresolved risks, pending follow-ups, or tool failures that meaningfully affect the next step.

# What To Omit
- Repetitive chatter, greetings, filler, and already-obvious phrasing.
- Raw transcripts or line-by-line retellings.
- Low-value details that are unlikely to matter again.

# Style
- Prefer durable engineering state over narrative.
- Mention concrete paths, commands, URLs, task ids, or tool names when they are important.
- If nothing meaningful should be carried forward, return a single bullet explaining that no durable checkpoint facts were found.
""";
}
