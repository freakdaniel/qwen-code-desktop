namespace QwenCode.App.Tools;

internal static class ToolContractCatalog
{
    /// <summary>
    /// Represents the Tool Contract
    /// </summary>
    /// <param name="Name">The name</param>
    /// <param name="DisplayName">The display name</param>
    /// <param name="Kind">The kind</param>
    /// <param name="IsImplemented">The is implemented</param>
    /// <param name="ContractPath">The contract path</param>
    public sealed record ToolContract(
        string Name,
        string DisplayName,
        string Kind,
        bool IsImplemented,
        string ContractPath);

    /// <summary>
    /// Gets the all
    /// </summary>
    public static IReadOnlyList<ToolContract> All { get; } =
    [
        new("read_file", "ReadFile", "read", true, "native://tools/read_file"),
        new("list_directory", "ListFiles", "read", true, "native://tools/list_directory"),
        new("glob", "Glob", "read", true, "native://tools/glob"),
        new("grep_search", "Grep", "read", true, "native://tools/grep_search"),
        new("run_shell_command", "Shell", "execute", true, "native://tools/run_shell_command"),
        new("write_file", "WriteFile", "modify", true, "native://tools/write_file"),
        new("edit", "Edit", "modify", true, "native://tools/edit"),
        new("todo_write", "TodoWrite", "modify", true, "native://tools/todo_write"),
        new("task_create", "TaskCreate", "modify", true, "native://tools/task_create"),
        new("task_list", "TaskList", "read", true, "native://tools/task_list"),
        new("task_get", "TaskGet", "read", true, "native://tools/task_get"),
        new("task_update", "TaskUpdate", "modify", true, "native://tools/task_update"),
        new("task_stop", "TaskStop", "modify", true, "native://tools/task_stop"),
        new("save_memory", "SaveMemory", "modify", true, "native://tools/save_memory"),
        new("agent", "Agent", "coordination", true, "native://tools/agent"),
        new("arena", "Arena", "coordination", true, "native://tools/arena"),
        new("skill", "Skill", "read", true, "native://tools/skill"),
        new("tool_search", "ToolSearch", "read", true, "native://tools/tool_search"),
        new("exit_plan_mode", "ExitPlanMode", "control", true, "native://tools/exit_plan_mode"),
        new("web_fetch", "WebFetch", "execute", true, "native://tools/web_fetch"),
        new("web_search", "WebSearch", "execute", true, "native://tools/web_search"),
        new("mcp-client", "McpClient", "read", true, "native://tools/mcp-client"),
        new("mcp-tool", "McpTool", "execute", true, "native://tools/mcp-tool"),
        new("lsp", "Lsp", "read", true, "native://tools/lsp"),
        new("ask_user_question", "AskUserQuestion", "coordination", true, "native://tools/ask_user_question"),
        new("cron_create", "CronCreate", "automation", true, "native://tools/cron_create"),
        new("cron_list", "CronList", "automation", true, "native://tools/cron_list"),
        new("cron_delete", "CronDelete", "automation", true, "native://tools/cron_delete")
    ];

    /// <summary>
    /// Gets the by name
    /// </summary>
    public static IReadOnlyDictionary<string, ToolContract> ByName { get; } =
        All.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the implemented
    /// </summary>
    public static IReadOnlyList<ToolContract> Implemented { get; } =
        All.Where(static item => item.IsImplemented).ToArray();
}
