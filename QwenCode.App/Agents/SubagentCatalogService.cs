using System.Text.RegularExpressions;
using QwenCode.App.Compatibility;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Agents;

public sealed partial class SubagentCatalogService(IDesktopEnvironmentPaths environmentPaths) : ISubagentCatalog
{
    public IReadOnlyList<SubagentDescriptor> ListAgents(WorkspacePaths paths)
    {
        var workspaceRoot = string.IsNullOrWhiteSpace(paths.WorkspaceRoot)
            ? Environment.CurrentDirectory
            : paths.WorkspaceRoot;
        var runtimeProfile = new QwenRuntimeProfileService(environmentPaths).Inspect(new WorkspacePaths
        {
            WorkspaceRoot = workspaceRoot
        });
        var userAgentsRoot = Path.Combine(environmentPaths.HomeDirectory, ".qwen", "agents");
        var projectAgentsRoot = Path.Combine(workspaceRoot, ".qwen", "agents");

        var agents = new List<SubagentDescriptor>();
        agents.AddRange(BuiltinSubagentRegistry.All);
        agents.AddRange(DiscoverMarkdownAgents(userAgentsRoot, "user"));
        if (runtimeProfile.IsWorkspaceTrusted)
        {
            agents.AddRange(DiscoverMarkdownAgents(projectAgentsRoot, "project"));
        }

        return agents
            .GroupBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(static agent => agent.Scope, ScopePriorityComparer.Instance)
                .First())
            .OrderBy(static agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public SubagentDescriptor? FindAgent(WorkspacePaths paths, string name) =>
        ListAgents(paths).FirstOrDefault(agent =>
            string.Equals(agent.Name, name, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<SubagentDescriptor> DiscoverMarkdownAgents(string rootPath, string scope)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories)
            .Select(path =>
            {
                var content = SafeReadAllText(path);
                var frontmatter = ParseFrontmatter(content);
                var body = StripFrontmatter(content).Trim();
                var relativeName = Path.GetFileNameWithoutExtension(path);
                var name = frontmatter.TryGetValue("name", out var explicitName) && !string.IsNullOrWhiteSpace(explicitName)
                    ? explicitName
                    : relativeName;

                return new SubagentDescriptor
                {
                    Name = name,
                    Description = frontmatter.TryGetValue("description", out var description) && !string.IsNullOrWhiteSpace(description)
                        ? description
                        : relativeName,
                    Scope = scope,
                    FilePath = path,
                    SystemPrompt = body,
                    Tools = ParseList(frontmatter, "tools")
                };
            })
            .ToArray();
    }

    private static string SafeReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string StripFrontmatter(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var match = FrontmatterRegex().Match(normalized);
        return match.Success
            ? normalized[(match.Index + match.Length)..]
            : normalized;
    }

    private static IReadOnlyDictionary<string, string> ParseFrontmatter(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var match = FrontmatterRegex().Match(normalized);
        if (!match.Success)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var yaml = match.Groups["yaml"].Value;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? activeListKey = null;
        var listValues = new List<string>();

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmedLine = line.TrimStart();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (trimmedLine.StartsWith("- ", StringComparison.Ordinal) && activeListKey is not null)
            {
                listValues.Add(trimmedLine[2..].Trim());
                result[activeListKey] = string.Join('\n', listValues);
                continue;
            }

            activeListKey = null;
            listValues.Clear();

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            result[key] = value;

            if (string.IsNullOrWhiteSpace(value))
            {
                activeListKey = key;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ParseList(IReadOnlyDictionary<string, string> frontmatter, string key) =>
        frontmatter.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

    [GeneratedRegex("^---\\n(?<yaml>[\\s\\S]*?)\\n---(?:\\n|$)", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();

    private sealed class ScopePriorityComparer : IComparer<string>
    {
        public static ScopePriorityComparer Instance { get; } = new();

        public int Compare(string? x, string? y) => GetPriority(x).CompareTo(GetPriority(y));

        private static int GetPriority(string? value) => value?.ToLowerInvariant() switch
        {
            "builtin" => 0,
            "user" => 1,
            "project" => 2,
            _ => 0
        };
    }
}
