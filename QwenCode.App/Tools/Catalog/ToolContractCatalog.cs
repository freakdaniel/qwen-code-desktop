namespace QwenCode.App.Tools;

internal static class ToolContractCatalog
{
    public sealed record ToolContract(
        string Name,
        string DisplayName,
        string Kind,
        bool IsImplemented,
        string ContractPath);

    public static IReadOnlyList<ToolContract> All { get; } =
    [
        new("read_file", "ReadFile", "read", true, "native://tools/read_file"),
        new("list_directory", "ListFiles", "read", true, "native://tools/list_directory"),
        new("glob", "Glob", "read", true, "native://tools/glob"),
        new("grep_search", "Grep", "read", true, "native://tools/grep_search"),
        new("run_shell_command", "Shell", "execute", true, "native://tools/run_shell_command"),
        new("write_file", "WriteFile", "modify", true, "native://tools/write_file"),
        new("edit", "Edit", "modify", true, "native://tools/edit"),
        new("todo_write", "TodoWrite", "modify", false, "native://tools/todo_write"),
        new("save_memory", "SaveMemory", "modify", false, "native://tools/save_memory"),
        new("agent", "Agent", "coordination", false, "native://tools/agent"),
        new("skill", "Skill", "coordination", false, "native://tools/skill"),
        new("exit_plan_mode", "ExitPlanMode", "control", false, "native://tools/exit_plan_mode"),
        new("web_fetch", "WebFetch", "execute", false, "native://tools/web_fetch"),
        new("web_search", "WebSearch", "execute", false, "native://tools/web_search"),
        new("lsp", "Lsp", "execute", false, "native://tools/lsp"),
        new("ask_user_question", "AskUserQuestion", "coordination", false, "native://tools/ask_user_question"),
        new("cron_create", "CronCreate", "automation", false, "native://tools/cron_create"),
        new("cron_list", "CronList", "automation", false, "native://tools/cron_list"),
        new("cron_delete", "CronDelete", "automation", false, "native://tools/cron_delete")
    ];

    public static IReadOnlyDictionary<string, ToolContract> ByName { get; } =
        All.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ToolContract> Implemented { get; } =
        All.Where(static item => item.IsImplemented).ToArray();
}
