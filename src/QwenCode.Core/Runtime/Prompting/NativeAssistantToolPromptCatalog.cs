namespace QwenCode.App.Runtime;

internal static class NativeAssistantToolPromptCatalog
{
    private static readonly IReadOnlyList<ToolPlaybook> Entries =
    [
        new(
            "tool_search",
            100,
            """
            ## `tool_search`
            - Use this first when the best native tool is unclear, especially in a large tool surface.
            - Search by intent or capability before guessing and burning a wrong tool call.
            - After finding a likely tool, switch to that tool directly instead of repeatedly calling `tool_search`.
            """),
        new(
            "web_search",
            110,
            """
            ## `web_search`
            - Use it to discover or compare sources for current facts, releases, documentation, news, or public information that may have changed.
            - Prefer primary and authoritative sources when they exist.
            - Use concrete years or version numbers in the query when recency matters.
            - If one search result looks stale or weak, refine the query and search again instead of answering from shaky evidence.
            """),
        new(
            "web_fetch",
            120,
            """
            ## `web_fetch`
            - Use it after you already know a promising URL and need the contents of that exact page.
            - Write the prompt around the specific facts you need from the page, not around a vague “summarize everything”.
            - If the URL 404s, redirects, or looks stale, recover by searching for a replacement source instead of treating the tool failure as the end of the task.
            """),
        new(
            "todo_write",
            130,
            """
            ## `todo_write`
            - Use it proactively for multi-step work so progress stays visible to both you and the user.
            - Keep exactly one task in progress whenever active work is underway.
            - Update the list immediately as tasks start or finish instead of batching stale status changes.
            """),
        new(
            "task_create",
            140,
            """
            ## `task_*`
            - Use `task_create`, `task_list`, `task_get`, `task_update`, and `task_stop` for richer orchestration across turns, tools, agents, or dependencies.
            - Use them when ownership, blockers, dependency edges, or longer-lived execution state matter more than a flat todo list.
            - Keep ownership and status explicit so handoffs between parent agent, subagent, and arena stay legible.
            """),
        new(
            "save_memory",
            150,
            """
            ## `save_memory`
            - Save only durable facts that will still matter in future turns: preferences, conventions, stable project rules, or recurring constraints.
            - Do not save transient blockers, one-off command output, temporary plans, or session-local discoveries that will go stale quickly.
            - Prefer project scope for repo-specific conventions and global scope for user-wide preferences.
            """),
        new(
            "lsp",
            160,
            """
            ## `lsp`
            - Prefer `lsp` over plain text search when you need semantic answers: definitions, implementations, references, symbols, diagnostics, or call hierarchy.
            - Use grep/glob to discover candidate files, then use `lsp` to disambiguate the code structure.
            - Reach for `lsp` when names are overloaded or when text search would be noisy or lossy.
            """),
        new(
            "mcp-client",
            170,
            """
            ## `mcp-client`
            - Use it for MCP discovery and higher-level workflows: inspect connected servers, list prompts/resources, and invoke named MCP prompts.
            - Start here when you know a server may help but you do not yet know the exact MCP tool call.
            - Prefer a matching MCP prompt over lower-level calls when the prompt clearly fits the task.
            """),
        new(
            "mcp-tool",
            180,
            """
            ## `mcp-tool`
            - Use it only after you know the exact server, tool, and arguments.
            - Do not guess MCP tool names or argument shapes when discovery is still needed; use `mcp-client` first.
            - If an MCP tool fails, keep the real server/tool/error details in context and decide whether discovery, different arguments, or a native tool is the better recovery path.
            """),
        new(
            "agent",
            190,
            """
            ## `agent`
            - Delegate bounded, high-value side work or parallelizable implementation, not the parent agent’s core understanding.
            - Write the brief like a smart engineer joining mid-task: include the goal, relevant files, constraints, known findings, and success criteria.
            - Keep final synthesis, tradeoff judgment, and the user-facing answer in the parent agent after the subagent returns.
            """),
        new(
            "arena",
            200,
            """
            ## `arena`
            - Use it for deliberate compare-and-choose work: second opinions, model competitions, or “pick the best implementation” tasks.
            - Do not default to arena for routine work that one strong agent can finish directly.
            - If the comparison is part of tracked work, link it to a task so the status and ownership stay coherent.
            """)
    ];

    public static IReadOnlyList<string> ResolvePlaybooks(
        NativeAssistantPromptCompositionContext context,
        int maxCount = 10)
    {
        if (context.AreToolsDisabled)
        {
            return [];
        }

        return Entries
            .Where(entry => context.CanUseTool(entry.ToolName))
            .OrderBy(entry => entry.Order)
            .Take(Math.Max(1, maxCount))
            .Select(static entry => entry.Prompt)
            .ToArray();
    }

    private sealed record ToolPlaybook(
        string ToolName,
        int Order,
        string Prompt);
}
