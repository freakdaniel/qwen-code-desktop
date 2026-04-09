using System.Text;
using System.Text.RegularExpressions;
using QwenCode.App.Models;

namespace QwenCode.App.Permissions;

internal static class PermissionRuleParser
{
    private static readonly IReadOnlyDictionary<string, string> ToolNameAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["run_shell_command"] = "run_shell_command",
            ["Shell"] = "run_shell_command",
            ["ShellTool"] = "run_shell_command",
            ["Bash"] = "run_shell_command",
            ["edit"] = "edit",
            ["Edit"] = "edit",
            ["EditTool"] = "edit",
            ["write_file"] = "write_file",
            ["Write"] = "write_file",
            ["WriteFile"] = "write_file",
            ["WriteFileTool"] = "write_file",
            ["read_file"] = "read_file",
            ["Read"] = "read_file",
            ["ReadFile"] = "read_file",
            ["ReadFileTool"] = "read_file",
            ["grep_search"] = "grep_search",
            ["Grep"] = "grep_search",
            ["GrepTool"] = "grep_search",
            ["search_file_content"] = "grep_search",
            ["SearchFiles"] = "grep_search",
            ["glob"] = "glob",
            ["Glob"] = "glob",
            ["GlobTool"] = "glob",
            ["FindFiles"] = "glob",
            ["list_directory"] = "list_directory",
            ["ListFiles"] = "list_directory",
            ["ListFilesTool"] = "list_directory",
            ["ReadFolder"] = "list_directory",
            ["save_memory"] = "save_memory",
            ["SaveMemory"] = "save_memory",
            ["SaveMemoryTool"] = "save_memory",
            ["todo_write"] = "todo_write",
            ["TodoWrite"] = "todo_write",
            ["TodoWriteTool"] = "todo_write",
            ["web_fetch"] = "web_fetch",
            ["WebFetch"] = "web_fetch",
            ["WebFetchTool"] = "web_fetch",
            ["web_search"] = "web_search",
            ["WebSearch"] = "web_search",
            ["WebSearchTool"] = "web_search",
            ["mcp-client"] = "mcp-client",
            ["McpClient"] = "mcp-client",
            ["McpClientTool"] = "mcp-client",
            ["mcp-tool"] = "mcp-tool",
            ["McpTool"] = "mcp-tool",
            ["McpToolTool"] = "mcp-tool",
            ["agent"] = "agent",
            ["Agent"] = "agent",
            ["AgentTool"] = "agent",
            ["task"] = "agent",
            ["Task"] = "agent",
            ["TaskTool"] = "agent",
            ["skill"] = "skill",
            ["Skill"] = "skill",
            ["SkillTool"] = "skill",
            ["exit_plan_mode"] = "exit_plan_mode",
            ["ExitPlanMode"] = "exit_plan_mode",
            ["ExitPlanModeTool"] = "exit_plan_mode",
            ["lsp"] = "lsp",
            ["Lsp"] = "lsp",
            ["LspTool"] = "lsp",
            ["replace"] = "edit"
        };

    private static readonly HashSet<string> ShellToolNames = ["run_shell_command"];

    private static readonly HashSet<string> ReadTools = ["read_file", "grep_search", "glob", "list_directory"];

    private static readonly HashSet<string> EditTools = ["edit", "write_file"];

    private static readonly HashSet<string> WebFetchTools = ["web_fetch"];

    /// <summary>
    /// Executes parse
    /// </summary>
    /// <param name="raw">The raw</param>
    /// <returns>The resulting permission rule</returns>
    public static PermissionRule Parse(string raw)
    {
        var trimmed = raw.Trim();
        var normalized = trimmed.Replace(":*", " *", StringComparison.Ordinal);
        var openParen = normalized.IndexOf('(');

        if (openParen < 0)
        {
            var canonicalName = ResolveToolName(normalized);
            return new PermissionRule
            {
                Raw = trimmed,
                ToolName = canonicalName
            };
        }

        var toolPart = normalized[..openParen].Trim();
        var specifier = normalized.EndsWith(")", StringComparison.Ordinal)
            ? normalized.Substring(openParen + 1, normalized.Length - openParen - 2)
            : normalized[(openParen + 1)..];
        var toolName = ResolveToolName(toolPart);

        return new PermissionRule
        {
            Raw = trimmed,
            ToolName = toolName,
            Specifier = specifier,
            SpecifierKind = GetSpecifierKind(toolName)
        };
    }

    /// <summary>
    /// Executes matches
    /// </summary>
    /// <param name="rule">The rule</param>
    /// <param name="context">The context</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public static bool Matches(PermissionRule rule, ApprovalCheckContext context)
    {
        var contextToolName = ResolveToolName(context.ToolName);
        if (!ToolMatchesRuleToolName(rule.ToolName, contextToolName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(rule.Specifier))
        {
            return true;
        }

        return (rule.SpecifierKind ?? GetSpecifierKind(rule.ToolName)) switch
        {
            "command" => MatchesCommand(rule.Specifier, context.Command),
            "path" => MatchesPath(rule.Specifier, context.FilePath, context.ProjectRoot, context.WorkingDirectory),
            "domain" => MatchesDomain(rule.Specifier, context.Domain),
            _ => MatchesLiteral(rule.Specifier, context.Specifier ?? context.Command)
        };
    }

    /// <summary>
    /// Resolves tool name
    /// </summary>
    /// <param name="rawName">The raw name</param>
    /// <returns>The resulting string</returns>
    public static string ResolveToolName(string rawName) =>
        ToolNameAliases.TryGetValue(rawName, out var canonical)
            ? canonical
            : rawName;

    internal static bool IsShellTool(string rawName) => ShellToolNames.Contains(ResolveToolName(rawName));

    internal static bool IsReadTool(string rawName) => ReadTools.Contains(ResolveToolName(rawName));

    internal static bool IsEditTool(string rawName) => EditTools.Contains(ResolveToolName(rawName));

    internal static bool IsWebFetchTool(string rawName) => WebFetchTools.Contains(ResolveToolName(rawName));

    private static string GetSpecifierKind(string canonicalToolName)
    {
        if (ShellToolNames.Contains(canonicalToolName))
        {
            return "command";
        }

        if (ReadTools.Contains(canonicalToolName) || EditTools.Contains(canonicalToolName))
        {
            return "path";
        }

        if (WebFetchTools.Contains(canonicalToolName))
        {
            return "domain";
        }

        return "literal";
    }

    private static bool ToolMatchesRuleToolName(string ruleToolName, string contextToolName)
    {
        if (string.Equals(ruleToolName, contextToolName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(ruleToolName, "read_file", StringComparison.OrdinalIgnoreCase) &&
            ReadTools.Contains(contextToolName))
        {
            return true;
        }

        if (string.Equals(ruleToolName, "edit", StringComparison.OrdinalIgnoreCase) &&
            EditTools.Contains(contextToolName))
        {
            return true;
        }

        if (ruleToolName.StartsWith("mcp__", StringComparison.OrdinalIgnoreCase) ||
            contextToolName.StartsWith("mcp__", StringComparison.OrdinalIgnoreCase))
        {
            return MatchesMcpPattern(ruleToolName, contextToolName);
        }

        return false;
    }

    private static bool MatchesCommand(string pattern, string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        return SplitCompoundCommand(command)
            .Any(segment => MatchesSingleCommandPattern(pattern, segment));
    }

    private static IReadOnlyList<string> SplitCompoundCommand(string command)
    {
        var commands = new List<string>();
        var builder = new StringBuilder();
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var escaped = false;

        for (var index = 0; index < command.Length; index++)
        {
            var character = command[index];

            if (escaped)
            {
                builder.Append(character);
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                builder.Append(character);
                escaped = true;
                continue;
            }

            if (character == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                builder.Append(character);
                continue;
            }

            if (character == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                builder.Append(character);
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && IsCommandBoundary(command, index, out var operatorLength))
            {
                var segment = builder.ToString().Trim();
                if (segment.Length > 0)
                {
                    commands.Add(segment);
                }

                builder.Clear();
                index += operatorLength - 1;
                continue;
            }

            builder.Append(character);
        }

        var tail = builder.ToString().Trim();
        if (tail.Length > 0)
        {
            commands.Add(tail);
        }

        return commands.Count > 0 ? commands : [command.Trim()];
    }

    private static bool IsCommandBoundary(string command, int index, out int operatorLength)
    {
        foreach (var @operator in new[] { "&&", "||", ";;", "|&", "|", ";" })
        {
            if (index + @operator.Length <= command.Length &&
                command.AsSpan(index, @operator.Length).Equals(@operator.AsSpan(), StringComparison.Ordinal))
            {
                operatorLength = @operator.Length;
                return true;
            }
        }

        operatorLength = 0;
        return false;
    }

    private static bool MatchesSingleCommandPattern(string pattern, string command)
    {
        if (string.Equals(pattern, "*", StringComparison.Ordinal))
        {
            return true;
        }

        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(command, pattern, StringComparison.Ordinal) ||
                   command.StartsWith(pattern + " ", StringComparison.Ordinal);
        }

        var regex = "^";
        var position = 0;
        while (position < pattern.Length)
        {
            var starIndex = pattern.IndexOf('*', position);
            if (starIndex < 0)
            {
                regex += Regex.Escape(pattern[position..]);
                break;
            }

            var literalBefore = pattern[position..starIndex];
            if (starIndex > 0 && pattern[starIndex - 1] == ' ')
            {
                regex += Regex.Escape(literalBefore[..^1]);
                regex += "( .*)?";
            }
            else
            {
                regex += Regex.Escape(literalBefore);
                regex += ".*";
            }

            position = starIndex + 1;
        }

        regex += "$";
        return new Regex(regex, RegexOptions.Compiled).IsMatch(command);
    }

    private static bool MatchesPath(
        string specifier,
        string? filePath,
        string? projectRoot,
        string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(filePath) ||
            string.IsNullOrWhiteSpace(projectRoot) ||
            string.IsNullOrWhiteSpace(workingDirectory))
        {
            return false;
        }

        var resolvedPattern = ResolvePathPattern(specifier, projectRoot, workingDirectory);
        var normalizedFilePath = NormalizePath(filePath);
        var regex = BuildGlobRegex(resolvedPattern);
        return regex.IsMatch(normalizedFilePath);
    }

    private static string ResolvePathPattern(string specifier, string projectRoot, string workingDirectory)
    {
        string resolvedPath;
        if (specifier.StartsWith("//", StringComparison.Ordinal))
        {
            resolvedPath = specifier[1..];
        }
        else if (specifier.StartsWith("~/", StringComparison.Ordinal) || specifier.StartsWith("~\\", StringComparison.Ordinal))
        {
            resolvedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                specifier[2..]);
        }
        else if (specifier.Length > 0 && specifier[0] == '/')
        {
            resolvedPath = Path.Combine(projectRoot, specifier.TrimStart('/'));
        }
        else if (specifier.StartsWith("./", StringComparison.Ordinal) || specifier.StartsWith(".\\", StringComparison.Ordinal))
        {
            resolvedPath = Path.Combine(workingDirectory, specifier[2..]);
        }
        else
        {
            resolvedPath = Path.Combine(workingDirectory, specifier);
        }

        return NormalizePath(Path.GetFullPath(resolvedPath));
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static Regex BuildGlobRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '*')
            {
                var isDoubleStar = index + 1 < pattern.Length && pattern[index + 1] == '*';
                if (isDoubleStar)
                {
                    builder.Append(".*");
                    index++;
                }
                else
                {
                    builder.Append(@"[^/]*");
                }
            }
            else if (character == '?')
            {
                builder.Append('.');
            }
            else
            {
                builder.Append(Regex.Escape(character.ToString()));
            }
        }

        builder.Append('$');
        return new Regex(
            builder.ToString(),
            RegexOptions.Compiled | (OperatingSystem.IsWindows() ? RegexOptions.IgnoreCase : RegexOptions.None));
    }

    private static bool MatchesDomain(string specifier, string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        var pattern = specifier.StartsWith("domain:", StringComparison.OrdinalIgnoreCase)
            ? specifier[7..].Trim()
            : specifier.Trim();
        if (pattern.Length == 0)
        {
            return false;
        }

        return string.Equals(domain, pattern, StringComparison.OrdinalIgnoreCase) ||
               domain.EndsWith("." + pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesLiteral(string specifier, string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(specifier, value, StringComparison.Ordinal);

    private static bool MatchesMcpPattern(string pattern, string toolName)
    {
        if (string.Equals(pattern, toolName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (pattern.EndsWith('*'))
        {
            var prefix = pattern[..^1];
            return toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        var patternParts = pattern.Split("__", StringSplitOptions.None);
        var toolParts = toolName.Split("__", StringSplitOptions.None);
        return patternParts.Length == 2 &&
               toolParts.Length >= 3 &&
               string.Equals(patternParts[0], toolParts[0], StringComparison.OrdinalIgnoreCase) &&
               string.Equals(patternParts[1], toolParts[1], StringComparison.OrdinalIgnoreCase);
    }
}
