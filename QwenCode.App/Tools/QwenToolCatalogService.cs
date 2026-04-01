using System.Text.RegularExpressions;
using QwenCode.App.Models;
using QwenCode.App.Compatibility;
using QwenCode.App.Permissions;

namespace QwenCode.App.Tools;

public sealed partial class QwenToolCatalogService(
    QwenRuntimeProfileService runtimeProfileService,
    IApprovalPolicyEngine approvalPolicyService) : IToolRegistry
{
    private static readonly IReadOnlyDictionary<string, string> SourceFileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["edit"] = "edit.ts",
        ["write_file"] = "write-file.ts",
        ["read_file"] = "read-file.ts",
        ["grep_search"] = "grep.ts",
        ["glob"] = "glob.ts",
        ["run_shell_command"] = "shell.ts",
        ["todo_write"] = "todoWrite.ts",
        ["save_memory"] = "memoryTool.ts",
        ["agent"] = "agent.ts",
        ["skill"] = "skill.ts",
        ["exit_plan_mode"] = "exitPlanMode.ts",
        ["web_fetch"] = "web-fetch.ts",
        ["web_search"] = "web-search/index.ts",
        ["list_directory"] = "ls.ts",
        ["lsp"] = "lsp.ts",
        ["ask_user_question"] = "askUserQuestion.ts",
        ["cron_create"] = "cron-create.ts",
        ["cron_list"] = "cron-list.ts",
        ["cron_delete"] = "cron-delete.ts"
    };

    public QwenToolCatalogSnapshot Inspect(SourceMirrorPaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var tools = LoadDescriptors(paths.QwenRoot, runtimeProfile);

        return new QwenToolCatalogSnapshot
        {
            SourceMode = tools.Count > 0 ? "source-assisted" : "empty",
            TotalCount = tools.Count,
            AllowedCount = tools.Count(tool => tool.ApprovalState == "allow"),
            AskCount = tools.Count(tool => tool.ApprovalState == "ask"),
            DenyCount = tools.Count(tool => tool.ApprovalState == "deny"),
            Tools = tools
        };
    }

    private IReadOnlyList<QwenToolDescriptor> LoadDescriptors(string qwenRoot, QwenRuntimeProfile runtimeProfile)
    {
        var toolsRoot = Path.Combine(qwenRoot, "packages", "core", "src", "tools");
        var namesPath = Path.Combine(toolsRoot, "tool-names.ts");
        if (!File.Exists(namesPath))
        {
            return [];
        }

        var content = File.ReadAllText(namesPath);
        var toolNames = ParseObject(content, "ToolNames");
        var displayNames = ParseObject(content, "ToolDisplayNames");

        return toolNames
            .OrderBy(static pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(pair =>
            {
                var approval = approvalPolicyService.Evaluate(
                    new ApprovalCheckContext
                    {
                        ToolName = pair.Value,
                        Kind = ClassifyKind(pair.Value),
                        ProjectRoot = runtimeProfile.ProjectRoot,
                        WorkingDirectory = runtimeProfile.ProjectRoot
                    },
                    runtimeProfile.ApprovalProfile);
                var relativeSourcePath = ResolveSourcePath(pair.Value);

                return new QwenToolDescriptor
                {
                    Name = pair.Value,
                    DisplayName = displayNames.TryGetValue(pair.Key, out var displayName)
                        ? displayName
                        : pair.Key,
                    Kind = ClassifyKind(pair.Value),
                    SourcePath = Path.Combine(toolsRoot, relativeSourcePath).Replace('\\', '/'),
                    ApprovalState = approval.State,
                    ApprovalReason = approval.Reason
                };
            })
            .ToArray();
    }

    private static Dictionary<string, string> ParseObject(string content, string objectName)
    {
        var match = ToolObjectRegex().Matches(content)
            .FirstOrDefault(candidate => string.Equals(candidate.Groups["name"].Value, objectName, StringComparison.Ordinal));
        if (match is null)
        {
            return [];
        }

        var body = match.Groups["body"].Value;
        return ToolEntryRegex()
            .Matches(body)
            .ToDictionary(
                static item => item.Groups["key"].Value,
                static item => item.Groups["value"].Value,
                StringComparer.Ordinal);
    }

    private static string ResolveSourcePath(string toolName) =>
        SourceFileMap.TryGetValue(toolName, out var path)
            ? path
            : $"{toolName}.ts";

    private static string ClassifyKind(string toolName) => toolName switch
    {
        "read_file" or "glob" or "grep_search" or "list_directory" => "read",
        "edit" or "write_file" or "todo_write" or "save_memory" => "modify",
        "run_shell_command" or "web_fetch" or "web_search" or "lsp" => "execute",
        "agent" or "skill" or "ask_user_question" => "coordination",
        "exit_plan_mode" => "control",
        "cron_create" or "cron_list" or "cron_delete" => "automation",
        _ => "other"
    };

    [GeneratedRegex(@"export const (?<name>\w+)\s*=\s*\{(?<body>.*?)\}\s*as const;", RegexOptions.Singleline)]
    private static partial Regex ToolObjectRegex();

    [GeneratedRegex(@"(?<key>\w+)\s*:\s*'(?<value>[^']+)'", RegexOptions.Singleline)]
    private static partial Regex ToolEntryRegex();
}
