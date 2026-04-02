using System.Text.Json;
using System.Text.RegularExpressions;
using QwenCode.App.Models;
using QwenCode.App.Infrastructure;

namespace QwenCode.App.Compatibility;

public sealed partial class QwenCompatibilityService(IDesktopEnvironmentPaths environmentPaths)
{
    private const string DefaultContextFileName = "QWEN.md";

    public QwenCompatibilitySnapshot Inspect(WorkspacePaths paths)
    {
        var projectRoot = string.IsNullOrWhiteSpace(paths.WorkspaceRoot)
            ? Environment.CurrentDirectory
            : paths.WorkspaceRoot;

        var projectQwenRoot = Path.Combine(projectRoot, ".qwen");
        var homeQwenRoot = Path.Combine(environmentPaths.HomeDirectory, ".qwen");
        var programDataRoot = ResolveProgramDataRoot();

        return new QwenCompatibilitySnapshot
        {
            ProjectRoot = projectRoot,
            DefaultContextFileName = DefaultContextFileName,
            SettingsLayers =
            [
                CreateLayer("system-defaults", "System defaults", "system defaults", 2, GetSystemDefaultsPath(programDataRoot)),
                CreateLayer("user-settings", "User settings", "user", 3, Path.Combine(homeQwenRoot, "settings.json")),
                CreateLayer("project-settings", "Project settings", "project", 4, Path.Combine(projectQwenRoot, "settings.json")),
                CreateLayer("system-settings", "System settings", "system override", 5, GetSystemSettingsPath(programDataRoot))
            ],
            SurfaceDirectories =
            [
                CreateSurfaceDirectory(
                    "project-commands",
                    "Project commands",
                    Path.Combine(projectQwenRoot, "commands"),
                    "Slash-command markdown and command surfaces"),
                CreateSurfaceDirectory(
                    "project-skills",
                    "Project skills",
                    Path.Combine(projectQwenRoot, "skills"),
                    "Project-local skills stored as directories with SKILL.md"),
                CreateSurfaceDirectory(
                    "user-skills",
                    "User skills",
                    Path.Combine(homeQwenRoot, "skills"),
                    "User-level skill surface shared across projects"),
                CreateSurfaceDirectory(
                    "context-root",
                    "Workspace context file",
                    Path.Combine(projectRoot, DefaultContextFileName),
                    "Default project instruction context file")
            ],
            Commands =
            [
                .. DiscoverCommands(Path.Combine(homeQwenRoot, "commands"), "user"),
                .. DiscoverCommands(Path.Combine(projectQwenRoot, "commands"), "project")
            ],
            Skills =
            [
                .. DiscoverSkills(Path.Combine(homeQwenRoot, "skills"), "user"),
                .. DiscoverSkills(Path.Combine(projectQwenRoot, "skills"), "project")
            ]
        };
    }

    private static QwenCompatibilityLayer CreateLayer(
        string id,
        string title,
        string scope,
        int priority,
        string path)
    {
        var exists = File.Exists(path);

        return new QwenCompatibilityLayer
        {
            Id = id,
            Title = title,
            Scope = scope,
            Priority = priority,
            Path = path,
            Exists = exists,
            Categories = exists ? ReadTopLevelCategories(path) : []
        };
    }

    private static QwenSurfaceDirectory CreateSurfaceDirectory(
        string id,
        string title,
        string path,
        string summary)
    {
        var fileExists = File.Exists(path);
        var directoryExists = Directory.Exists(path);
        var exists = fileExists || directoryExists;
        var itemCount = directoryExists
            ? Directory.EnumerateFileSystemEntries(path).Count()
            : fileExists
                ? 1
                : 0;

        return new QwenSurfaceDirectory
        {
            Id = id,
            Title = title,
            Path = path,
            Exists = exists,
            ItemCount = itemCount,
            Summary = exists
                ? $"{summary}. Detected {itemCount} item(s)."
                : $"{summary}. Not found yet."
        };
    }

    private string ResolveProgramDataRoot() =>
        Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH") is { Length: > 0 } overridePath
            ? Path.GetDirectoryName(overridePath) ?? string.Empty
            : environmentPaths.ProgramDataDirectory is { Length: > 0 } commonAppData
                ? Path.Combine(commonAppData, "qwen-code")
                : string.Empty;

    private static string GetSystemDefaultsPath(string programDataRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_DEFAULTS_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(programDataRoot, "system-defaults.json")
            : overridePath;
    }

    private static string GetSystemSettingsPath(string programDataRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(programDataRoot, "settings.json")
            : overridePath;
    }

    private static IReadOnlyList<string> ReadTopLevelCategories(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);

            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject()
                    .Select(property => property.Name)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<QwenCommandSurface> DiscoverCommands(string rootPath, string scope)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories)
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(rootPath, path);
                var group = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? string.Empty;
                var content = ReadText(path);
                var frontmatter = ParseFrontmatter(content);
                var name = Path.GetFileNameWithoutExtension(relativePath).Replace('\\', '/');

                return new QwenCommandSurface
                {
                    Id = $"{scope}:{relativePath.Replace('\\', '/').ToLowerInvariant()}",
                    Name = string.IsNullOrWhiteSpace(group) ? name : $"{group}/{name}",
                    Scope = scope,
                    Path = path,
                    Description = ResolveDescription(frontmatter, content, name),
                    Group = string.IsNullOrWhiteSpace(group) ? "root" : group
                };
            })
            .OrderBy(static item => item.Scope, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<QwenSkillSurface> DiscoverSkills(string rootPath, string scope)
    {
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        return Directory.EnumerateDirectories(rootPath)
            .Select(path => Path.Combine(path, "SKILL.md"))
            .Where(File.Exists)
            .Select(path =>
            {
                var relativeDirectory = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
                var content = ReadText(path);
                var frontmatter = ParseFrontmatter(content);
                var name = frontmatter.TryGetValue("name", out var explicitName) && !string.IsNullOrWhiteSpace(explicitName)
                    ? explicitName
                    : relativeDirectory;

                return new QwenSkillSurface
                {
                    Id = $"{scope}:{relativeDirectory.ToLowerInvariant()}",
                    Name = name,
                    Scope = scope,
                    Path = path,
                    Description = ResolveDescription(frontmatter, content, relativeDirectory),
                    AllowedTools = ParseAllowedTools(frontmatter)
                };
            })
            .OrderBy(static item => item.Scope, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ReadText(string path)
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

    private static IReadOnlyDictionary<string, string> ParseFrontmatter(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var match = FrontmatterRegex().Match(content.Replace("\r\n", "\n", StringComparison.Ordinal));
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
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) && activeListKey is not null)
            {
                listValues.Add(line[2..].Trim());
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

    private static string ResolveDescription(
        IReadOnlyDictionary<string, string> frontmatter,
        string content,
        string fallbackName)
    {
        if (frontmatter.TryGetValue("description", out var description) &&
            !string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        foreach (var rawLine in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) ||
                line == "---" ||
                line.StartsWith("name:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith('#'))
            {
                return line.TrimStart('#', ' ');
            }

            return line.Length > 180 ? line[..180] : line;
        }

        return fallbackName;
    }

    private static IReadOnlyList<string> ParseAllowedTools(IReadOnlyDictionary<string, string> frontmatter) =>
        frontmatter.TryGetValue("allowedTools", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

    [GeneratedRegex("^---\\n(?<yaml>[\\s\\S]*?)\\n---(?:\\n|$)", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}
