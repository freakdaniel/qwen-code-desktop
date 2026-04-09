using System.Text.Json;
using QwenCode.App.Models;
using QwenCode.App.Tools;

namespace QwenCode.App.Permissions;

/// <summary>
/// Builds project-scoped permission rules from pending approval entries.
/// </summary>
public static class ApprovalRuleSuggestionService
{
    /// <summary>
    /// Creates the narrowest practical allow rule for the pending tool entry.
    /// </summary>
    /// <param name="pendingTool">The pending approval entry.</param>
    /// <param name="projectRoot">The project root for relative permission rules.</param>
    /// <returns>A qwen-compatible allow rule when one can be derived.</returns>
    public static string? SuggestProjectAllowRule(DesktopSessionEntry pendingTool, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(pendingTool.ToolName))
        {
            return null;
        }

        var canonicalToolName = PermissionRuleParser.ResolveToolName(pendingTool.ToolName);
        using var document = ParseArguments(pendingTool.Arguments);
        var arguments = document?.RootElement;

        if (string.Equals(canonicalToolName, "run_shell_command", StringComparison.OrdinalIgnoreCase))
        {
            return BuildShellRule(arguments, pendingTool.WorkingDirectory, projectRoot);
        }

        if (string.Equals(canonicalToolName, "mcp-tool", StringComparison.OrdinalIgnoreCase))
        {
            return BuildMcpRule(arguments);
        }

        if (string.Equals(canonicalToolName, "agent", StringComparison.OrdinalIgnoreCase))
        {
            return BuildLiteralRule("Agent", arguments, "agent_type");
        }

        if (string.Equals(canonicalToolName, "skill", StringComparison.OrdinalIgnoreCase))
        {
            return BuildLiteralRule("Skill", arguments, "skill_name");
        }

        if (string.Equals(canonicalToolName, "exit_plan_mode", StringComparison.OrdinalIgnoreCase))
        {
            return "ExitPlanMode";
        }

        if (string.Equals(canonicalToolName, "web_fetch", StringComparison.OrdinalIgnoreCase))
        {
            return BuildDomainRule(arguments);
        }

        if (!ToolContractCatalog.ByName.TryGetValue(canonicalToolName, out var toolContract))
        {
            return canonicalToolName;
        }

        if (toolContract.Kind is "read" or "modify")
        {
            return BuildPathRule(
                ResolvePathRulePrefix(canonicalToolName),
                arguments,
                projectRoot,
                includeChildrenForDirectories: string.Equals(canonicalToolName, "list_directory", StringComparison.OrdinalIgnoreCase));
        }

        return toolContract.DisplayName;
    }

    private static JsonDocument? ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(argumentsJson);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildShellRule(JsonElement? arguments, string workingDirectory, string projectRoot)
    {
        if (arguments is not JsonElement element ||
            !TryGetString(element, "command", out var command) ||
            string.IsNullOrWhiteSpace(command))
        {
            return "Bash";
        }

        var shellWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? projectRoot : workingDirectory;
        var operationRules = ShellOperationAnalyzer.ExtractOperations(command, shellWorkingDirectory)
            .Select(operation => BuildVirtualOperationRule(operation, projectRoot))
            .Where(static rule => !string.IsNullOrWhiteSpace(rule))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (operationRules.Length == 1)
        {
            return operationRules[0]!;
        }

        var firstSegment = SplitFirstCommandSegment(command);
        if (string.IsNullOrWhiteSpace(firstSegment))
        {
            return "Bash";
        }

        var executable = ExtractExecutable(firstSegment);
        if (string.IsNullOrWhiteSpace(executable))
        {
            return $"Bash({firstSegment.Trim()})";
        }

        return string.Equals(firstSegment.Trim(), executable, StringComparison.Ordinal)
            ? $"Bash({executable})"
            : $"Bash({executable} *)";
    }

    private static string? BuildVirtualOperationRule(ShellOperation operation, string projectRoot)
    {
        var canonicalToolName = PermissionRuleParser.ResolveToolName(operation.VirtualTool);
        if (PermissionRuleParser.IsReadTool(canonicalToolName))
        {
            if (string.Equals(canonicalToolName, "list_directory", StringComparison.OrdinalIgnoreCase))
            {
                return BuildResolvedPathRule("Read", operation.FilePath, projectRoot, includeChildrenForDirectories: true);
            }

            return BuildResolvedPathRule("Read", operation.FilePath, projectRoot, includeChildrenForDirectories: false);
        }

        if (string.Equals(canonicalToolName, "write_file", StringComparison.OrdinalIgnoreCase))
        {
            return BuildResolvedPathRule("Write", operation.FilePath, projectRoot, includeChildrenForDirectories: false);
        }

        if (PermissionRuleParser.IsEditTool(canonicalToolName))
        {
            return BuildResolvedPathRule("Edit", operation.FilePath, projectRoot, includeChildrenForDirectories: false);
        }

        if (PermissionRuleParser.IsWebFetchTool(canonicalToolName))
        {
            return string.IsNullOrWhiteSpace(operation.Domain)
                ? "WebFetch"
                : $"WebFetch(domain:{operation.Domain})";
        }

        return null;
    }

    private static string ResolvePathRulePrefix(string canonicalToolName)
    {
        if (PermissionRuleParser.IsReadTool(canonicalToolName))
        {
            return "Read";
        }

        return string.Equals(canonicalToolName, "write_file", StringComparison.OrdinalIgnoreCase) ? "Write" : "Edit";
    }

    private static string BuildPathRule(
        string alias,
        JsonElement? arguments,
        string projectRoot,
        bool includeChildrenForDirectories)
    {
        if (arguments is not JsonElement element)
        {
            return alias;
        }

        var candidatePath = TryExtractPath(element);
        return BuildResolvedPathRule(alias, candidatePath, projectRoot, includeChildrenForDirectories) ?? alias;
    }

    private static string? BuildResolvedPathRule(
        string alias,
        string? candidatePath,
        string projectRoot,
        bool includeChildrenForDirectories)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return alias;
        }

        var resolvedPath = ResolvePath(candidatePath, projectRoot);
        var normalizedProjectRoot = NormalizePath(Path.GetFullPath(projectRoot)).TrimEnd('/');
        var normalizedResolvedPath = NormalizePath(resolvedPath);
        var relativePrefix = normalizedResolvedPath.StartsWith(normalizedProjectRoot + "/", OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal)
            ? "/" + normalizedResolvedPath[(normalizedProjectRoot.Length + 1)..]
            : normalizedResolvedPath.StartsWith(normalizedProjectRoot, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal)
                ? "/"
                : "//" + normalizedResolvedPath;

        var shouldIncludeChildren = includeChildrenForDirectories ||
            candidatePath.EndsWith(Path.DirectorySeparatorChar) ||
            candidatePath.EndsWith(Path.AltDirectorySeparatorChar) ||
            Directory.Exists(resolvedPath);

        if (shouldIncludeChildren && !relativePrefix.EndsWith("/**", StringComparison.Ordinal))
        {
            relativePrefix = relativePrefix.TrimEnd('/') + "/**";
        }

        return $"{alias}({relativePrefix})";
    }

    private static string BuildDomainRule(JsonElement? arguments)
    {
        if (arguments is not JsonElement element ||
            !TryGetString(element, "url", out var url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return "WebFetch";
        }

        return $"WebFetch(domain:{uri.Host})";
    }

    private static string BuildMcpRule(JsonElement? arguments)
    {
        if (arguments is not JsonElement element)
        {
            return "mcp-tool";
        }

        if (!TryGetString(element, "server_name", out var serverName) ||
            !TryGetString(element, "tool_name", out var toolName))
        {
            return "mcp-tool";
        }

        return $"mcp__{serverName}__{toolName}";
    }

    private static string BuildLiteralRule(string alias, JsonElement? arguments, string key)
    {
        if (arguments is not JsonElement element || !TryGetString(element, key, out var value))
        {
            return alias;
        }

        return $"{alias}({value})";
    }

    private static string? TryExtractPath(JsonElement arguments)
    {
        if (TryGetString(arguments, "file_path", out var filePath))
        {
            return filePath;
        }

        if (TryGetString(arguments, "path", out var path))
        {
            return path;
        }

        if (TryGetString(arguments, "directory", out var directory))
        {
            return directory;
        }

        return null;
    }

    private static string ResolvePath(string candidatePath, string projectRoot)
    {
        if (Path.IsPathRooted(candidatePath))
        {
            return Path.GetFullPath(candidatePath);
        }

        return Path.GetFullPath(Path.Combine(projectRoot, candidatePath));
    }

    private static string NormalizePath(string value) => value.Replace('\\', '/');

    private static string SplitFirstCommandSegment(string command)
    {
        var inSingleQuote = false;
        var inDoubleQuote = false;

        for (var index = 0; index < command.Length; index++)
        {
            var character = command[index];

            if (character == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (character == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (inSingleQuote || inDoubleQuote)
            {
                continue;
            }

            foreach (var token in new[] { "&&", "||", ";;", "|&", "|", ";" })
            {
                if (index + token.Length <= command.Length &&
                    string.Equals(command.Substring(index, token.Length), token, StringComparison.Ordinal))
                {
                    return command[..index].Trim();
                }
            }
        }

        return command.Trim();
    }

    private static string ExtractExecutable(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return string.Empty;
        }

        var trimmed = segment.Trim();
        string token;
        if (trimmed[0] is '"' or '\'')
        {
            var quote = trimmed[0];
            var endQuote = trimmed.IndexOf(quote, 1);
            token = endQuote > 0 ? trimmed[1..endQuote] : trimmed[1..];
        }
        else
        {
            var whitespaceIndex = trimmed.IndexOfAny([' ', '\t', '\r', '\n']);
            token = whitespaceIndex < 0 ? trimmed : trimmed[..whitespaceIndex];
        }

        if (token.Contains('/') || token.Contains('\\'))
        {
            token = Path.GetFileName(token);
            token = Path.GetFileNameWithoutExtension(token);
        }

        return token.Trim();
    }

    private static bool TryGetString(JsonElement element, string key, out string value)
    {
        if (element.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
