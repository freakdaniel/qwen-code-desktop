using System.Text.RegularExpressions;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;

namespace QwenCode.Core.Agents;

/// <summary>
/// Represents the Subagent Catalog Service
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="validationService">The validation service</param>
public sealed partial class SubagentCatalogService(
    IDesktopEnvironmentPaths environmentPaths,
    ISubagentValidationService validationService) : ISubagentCatalog
{
    private readonly ISubagentValidationService _validationService = validationService;

    /// <summary>
    /// Lists agents
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting i read only list subagent descriptor</returns>
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

    /// <summary>
    /// Executes find agent
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="name">The name</param>
    /// <returns>The resulting subagent descriptor?</returns>
    public SubagentDescriptor? FindAgent(WorkspacePaths paths, string name) =>
        ListAgents(paths).FirstOrDefault(agent =>
            string.Equals(agent.Name, name, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<SubagentDescriptor> DiscoverMarkdownAgents(string rootPath, string scope)
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
                    Tools = ParseList(frontmatter, "tools"),
                    Model = frontmatter.TryGetValue("model", out var model) ? model : string.Empty,
                    Color = frontmatter.TryGetValue("color", out var color) ? color : string.Empty,
                    RunConfiguration = ParseRunConfiguration(frontmatter)
                };
            })
            .Select(descriptor =>
            {
                var validation = _validationService.Validate(descriptor);
                return validation.IsValid
                    ? new SubagentDescriptor
                    {
                        Name = descriptor.Name,
                        Description = descriptor.Description,
                        Scope = descriptor.Scope,
                        FilePath = descriptor.FilePath,
                        SystemPrompt = descriptor.SystemPrompt,
                        IsBuiltin = descriptor.IsBuiltin,
                        Tools = descriptor.Tools,
                        Model = descriptor.Model,
                        Color = descriptor.Color,
                        RunConfiguration = descriptor.RunConfiguration,
                        ValidationWarnings = validation.Warnings
                    }
                    : null;
            })
            .Where(static descriptor => descriptor is not null)
            .Cast<SubagentDescriptor>()
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
        string? activeObjectKey = null;
        var listValues = new List<string>();

        foreach (var rawLine in yaml.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmedLine = line.TrimStart();
            var indentation = line.Length - trimmedLine.Length;
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

            if (indentation > 0 && activeObjectKey is not null)
            {
                var nestedSeparatorIndex = trimmedLine.IndexOf(':');
                if (nestedSeparatorIndex > 0)
                {
                    var nestedKey = trimmedLine[..nestedSeparatorIndex].Trim();
                    var nestedValue = trimmedLine[(nestedSeparatorIndex + 1)..].Trim().Trim('"');
                    result[$"{activeObjectKey}.{nestedKey}"] = nestedValue;
                    continue;
                }
            }

            activeListKey = null;
            activeObjectKey = null;
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
                activeObjectKey = key;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ParseList(IReadOnlyDictionary<string, string> frontmatter, string key) =>
        frontmatter.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

    private static SubagentRunConfiguration ParseRunConfiguration(IReadOnlyDictionary<string, string> frontmatter) =>
        new()
        {
            MaxTimeMinutes = TryParseInt(frontmatter, "runConfig.max_time_minutes") ?? TryParseInt(frontmatter, "max_time_minutes"),
            MaxTurns = TryParseInt(frontmatter, "runConfig.max_turns") ?? TryParseInt(frontmatter, "max_turns")
        };

    private static int? TryParseInt(IReadOnlyDictionary<string, string> frontmatter, string key) =>
        frontmatter.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : null;

    [GeneratedRegex("^---\\n(?<yaml>[\\s\\S]*?)\\n---(?:\\n|$)", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();

    private sealed class ScopePriorityComparer : IComparer<string>
    {
        /// <summary>
        /// Gets the instance
        /// </summary>
        public static ScopePriorityComparer Instance { get; } = new();

        /// <summary>
        /// Executes compare
        /// </summary>
        /// <param name="x">The x</param>
        /// <param name="y">The y</param>
        /// <returns>The resulting int</returns>
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
