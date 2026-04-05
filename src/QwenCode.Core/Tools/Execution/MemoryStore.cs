using QwenCode.App.Models;

namespace QwenCode.App.Tools;

internal static class MemoryStore
{
    internal const string MemorySectionHeader = "## Qwen Added Memories";

    /// <summary>
    /// Resolves memory file path
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="scope">The scope</param>
    /// <returns>The resulting string</returns>
    public static string ResolveMemoryFilePath(QwenRuntimeProfile runtimeProfile, string? scope)
    {
        var effectiveScope = NormalizeScope(scope);
        var root = string.Equals(effectiveScope, "global", StringComparison.OrdinalIgnoreCase)
            ? runtimeProfile.GlobalQwenDirectory
            : runtimeProfile.ProjectRoot;
        var fileName = runtimeProfile.ContextFileNames.FirstOrDefault(static name => !string.IsNullOrWhiteSpace(name))
            ?? "QWEN.md";

        return Path.Combine(root, fileName);
    }

    /// <summary>
    /// Saves fact async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="fact">The fact</param>
    /// <param name="scope">The scope</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
    public static async Task<string> SaveFactAsync(
        QwenRuntimeProfile runtimeProfile,
        string fact,
        string? scope,
        CancellationToken cancellationToken)
    {
        var targetPath = ResolveMemoryFilePath(runtimeProfile, scope);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        var currentContent = File.Exists(targetPath)
            ? await File.ReadAllTextAsync(targetPath, cancellationToken)
            : string.Empty;
        var updatedContent = ComputeUpdatedContent(currentContent, fact);

        await File.WriteAllTextAsync(targetPath, updatedContent, cancellationToken);
        return targetPath;
    }

    private static string ComputeUpdatedContent(string currentContent, string fact)
    {
        var normalizedFact = NormalizeFact(fact);
        var newMemoryItem = $"- {normalizedFact}";
        var headerIndex = currentContent.IndexOf(MemorySectionHeader, StringComparison.Ordinal);

        if (headerIndex < 0)
        {
            var separator = EnsureNewlineSeparation(currentContent);
            return $"{currentContent}{separator}{MemorySectionHeader}{Environment.NewLine}{newMemoryItem}{Environment.NewLine}";
        }

        var startOfSectionContent = headerIndex + MemorySectionHeader.Length;
        var endOfSectionIndex = currentContent.IndexOf(
            $"{Environment.NewLine}## ",
            startOfSectionContent,
            StringComparison.Ordinal);
        if (endOfSectionIndex < 0)
        {
            endOfSectionIndex = currentContent.Length;
        }

        var beforeSectionMarker = currentContent[..startOfSectionContent].TrimEnd();
        var sectionContent = currentContent[startOfSectionContent..endOfSectionIndex].TrimEnd();
        var afterSectionMarker = currentContent[endOfSectionIndex..];

        sectionContent += $"{Environment.NewLine}{newMemoryItem}";
        return $"{beforeSectionMarker}{Environment.NewLine}{sectionContent.TrimStart()}{Environment.NewLine}{afterSectionMarker}".TrimEnd() +
               Environment.NewLine;
    }

    private static string NormalizeFact(string fact)
    {
        var trimmed = fact.Trim();
        while (trimmed.StartsWith("-", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..].TrimStart();
        }

        return trimmed;
    }

    private static string NormalizeScope(string? scope) =>
        string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase) ? "global" : "project";

    private static string EnsureNewlineSeparation(string currentContent)
    {
        if (currentContent.Length == 0)
        {
            return string.Empty;
        }

        if (currentContent.EndsWith($"{Environment.NewLine}{Environment.NewLine}", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return currentContent.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? Environment.NewLine
            : $"{Environment.NewLine}{Environment.NewLine}";
    }
}
