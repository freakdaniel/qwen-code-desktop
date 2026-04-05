using System.Text.RegularExpressions;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

/// <summary>
/// Represents the File Discovery Service
/// </summary>
/// <param name="gitCliService">The git cli service</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
public sealed class FileDiscoveryService(
    IGitCliService gitCliService,
    QwenRuntimeProfileService runtimeProfileService) : IFileDiscoveryService
{
    private static readonly string[] IgnoredDirectories =
    [
        ".git",
        ".qwen",
        "node_modules",
        "bin",
        "obj",
        ".electron"
    ];

    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting file discovery snapshot</returns>
    public FileDiscoverySnapshot Inspect(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var qwenIgnorePath = Path.Combine(runtimeProfile.ProjectRoot, ".qwenignore");
        var qwenIgnoreRules = LoadIgnoreRules(qwenIgnorePath);
        var contextFiles = runtimeProfile.ContextFilePaths
            .Where(File.Exists)
            .Select(path => ToRelativeUnixPath(runtimeProfile.ProjectRoot, path))
            .ToArray();

        var repositoryRootResult = gitCliService.Run(runtimeProfile.ProjectRoot, "rev-parse", "--show-toplevel");
        var gitAware = repositoryRootResult.Success;

        IReadOnlyList<string> candidateFiles;
        IReadOnlyList<string> gitIgnoredFiles;
        if (gitAware)
        {
            candidateFiles = ReadGitFileList(runtimeProfile.ProjectRoot, "ls-files", "--cached", "--others", "--exclude-standard");
            gitIgnoredFiles = ReadGitFileList(runtimeProfile.ProjectRoot, "ls-files", "--others", "--ignored", "--exclude-standard");
        }
        else
        {
            candidateFiles = EnumerateWorkspaceFiles(runtimeProfile.ProjectRoot);
            gitIgnoredFiles = [];
        }

        var qwenIgnoredFiles = candidateFiles
            .Where(path => ShouldIgnore(path, qwenIgnoreRules))
            .ToArray();
        var visibleFiles = candidateFiles
            .Except(qwenIgnoredFiles, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FileDiscoverySnapshot
        {
            GitAware = gitAware,
            HasQwenIgnore = qwenIgnoreRules.Count > 0,
            CandidateFileCount = candidateFiles.Count,
            VisibleFileCount = visibleFiles.Length,
            GitIgnoredCount = gitIgnoredFiles.Count,
            QwenIgnoredCount = qwenIgnoredFiles.Length,
            QwenIgnorePatternCount = qwenIgnoreRules.Count,
            ContextFiles = contextFiles,
            SampleVisibleFiles = visibleFiles.Take(10).ToArray(),
            SampleGitIgnoredFiles = gitIgnoredFiles
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray(),
            SampleQwenIgnoredFiles = qwenIgnoredFiles
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToArray()
        };
    }

    private IReadOnlyList<string> ReadGitFileList(string workingDirectory, params string[] arguments)
    {
        var result = gitCliService.Run(workingDirectory, arguments);
        return result.Success
            ? result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeRelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
    }

    private static IReadOnlyList<string> EnumerateWorkspaceFiles(string projectRoot)
    {
        var results = new List<string>();
        EnumerateRecursive(projectRoot, projectRoot, results);
        return results;
    }

    private static void EnumerateRecursive(string root, string current, ICollection<string> results)
    {
        foreach (var directory in Directory.EnumerateDirectories(current))
        {
            var directoryName = Path.GetFileName(directory);
            if (IgnoredDirectories.Contains(directoryName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            EnumerateRecursive(root, directory, results);
        }

        foreach (var filePath in Directory.EnumerateFiles(current))
        {
            results.Add(ToRelativeUnixPath(root, filePath));
        }
    }

    private static IReadOnlyList<IgnoreRule> LoadIgnoreRules(string qwenIgnorePath)
    {
        if (!File.Exists(qwenIgnorePath))
        {
            return [];
        }

            return File.ReadAllLines(qwenIgnorePath)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#", StringComparison.Ordinal))
            .Select(CreateIgnoreRule)
            .ToArray();
    }

    private static IgnoreRule CreateIgnoreRule(string rawPattern)
    {
        var negated = rawPattern.StartsWith("!", StringComparison.Ordinal);
        var normalized = negated ? rawPattern[1..] : rawPattern;
        normalized = normalized.Replace('\\', '/');
        normalized = normalized.StartsWith("./", StringComparison.Ordinal)
            ? normalized[2..]
            : normalized;

        var directoryOnly = normalized.EndsWith("/", StringComparison.Ordinal);
        normalized = directoryOnly ? normalized.TrimEnd('/') : normalized;
        var regex = BuildPatternRegex(normalized, directoryOnly);

        return new IgnoreRule(negated, regex);
    }

    private static bool ShouldIgnore(string relativePath, IReadOnlyList<IgnoreRule> rules)
    {
        var ignored = false;
        foreach (var rule in rules)
        {
            if (!rule.Pattern.IsMatch(relativePath))
            {
                continue;
            }

            ignored = !rule.Negated;
        }

        return ignored;
    }

    private static Regex BuildPatternRegex(string pattern, bool directoryOnly)
    {
        var escaped = Regex.Escape(pattern)
            .Replace(@"\*\*", "___DOUBLE_STAR___", StringComparison.Ordinal)
            .Replace(@"\*", "___STAR___", StringComparison.Ordinal)
            .Replace(@"\?", "___QUESTION___", StringComparison.Ordinal);
        escaped = escaped
            .Replace("___DOUBLE_STAR___", ".*", StringComparison.Ordinal)
            .Replace("___STAR___", "[^/]*", StringComparison.Ordinal)
            .Replace("___QUESTION___", "[^/]", StringComparison.Ordinal);

        var anchoredPattern = pattern.Contains('/', StringComparison.Ordinal)
            ? $"(?:^|.*/){escaped}"
            : $"(?:^|.*/){escaped}";
        var suffix = directoryOnly ? "(?:/.*)?$" : "$";
        return CreateIgnoreRegex($"{anchoredPattern}{suffix}");
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').Trim();

    private static string ToRelativeUnixPath(string root, string fullPath) =>
        NormalizeRelativePath(Path.GetRelativePath(root, fullPath));

    private sealed record IgnoreRule(bool Negated, Regex Pattern);

    private static Regex CreateIgnoreRegex(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
