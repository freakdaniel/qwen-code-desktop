using QwenCode.Core.Models;

namespace QwenCode.Core.Agents;

internal static class BuiltinSubagentRegistry
{
    /// <summary>
    /// Gets the all
    /// </summary>
    public static IReadOnlyList<SubagentDescriptor> All { get; } =
    [
        new()
        {
            Name = "general-purpose",
            Description = "General-purpose agent for researching complex questions, searching code, and handling multi-step tasks.",
            Scope = "builtin",
            FilePath = "<builtin:general-purpose>",
            IsBuiltin = true,
            SystemPrompt = "General-purpose agent that researches complex questions and reports concise findings.",
            Tools = ["read_file", "glob", "grep_search", "run_shell_command", "web_fetch", "web_search", "lsp", "ask_user_question"]
        },
        new()
        {
            Name = "Explore",
            Description = "Fast exploration agent specialized for codebase search, navigation, and focused technical reconnaissance.",
            Scope = "builtin",
            FilePath = "<builtin:Explore>",
            IsBuiltin = true,
            SystemPrompt = "Read-only exploration agent that searches broadly, narrows findings, and reports only the load-bearing evidence.",
            Tools = ["read_file", "glob", "grep_search", "list_directory", "run_shell_command", "web_fetch", "web_search", "todo_write", "save_memory", "lsp", "ask_user_question"]
        }
    ];
}
