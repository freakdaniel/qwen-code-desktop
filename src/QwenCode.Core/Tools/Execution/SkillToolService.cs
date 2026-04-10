using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

/// <summary>
/// Represents the Skill Tool Service
/// </summary>
/// <param name="compatibilityService">The compatibility service</param>
public sealed partial class SkillToolService(QwenCompatibilityService compatibilityService) : ISkillToolService
{
    /// <summary>
    /// Loads skill content async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
    public async Task<string> LoadSkillContentAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var skillName = TryGetSkillName(arguments);
        if (string.IsNullOrWhiteSpace(skillName))
        {
            throw new InvalidOperationException("Parameter 'skill' is required.");
        }

        var snapshot = compatibilityService.Inspect(new WorkspacePaths
        {
            WorkspaceRoot = runtimeProfile.ProjectRoot
        });

        var skill = snapshot.Skills
            .OrderByDescending(static item => GetScopePriority(item.Scope))
            .FirstOrDefault(item => string.Equals(item.Name, skillName, StringComparison.OrdinalIgnoreCase));

        if (skill is null)
        {
            var availableSkills = snapshot.Skills
                .Select(static item => item.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (availableSkills.Length == 0)
            {
                throw new InvalidOperationException($"Skill '{skillName}' not found. No skills are currently available.");
            }

            throw new InvalidOperationException(
                $"Skill '{skillName}' not found. Available skills: {string.Join(", ", availableSkills)}");
        }

        if (!File.Exists(skill.Path))
        {
            throw new InvalidOperationException($"Skill '{skill.Name}' is registered but its file is missing.");
        }

        var body = ExtractBody(await File.ReadAllTextAsync(skill.Path, cancellationToken));
        var baseDirectory = Path.GetDirectoryName(skill.Path) ?? runtimeProfile.ProjectRoot;
        var builder = new StringBuilder();
        builder.AppendLine($"Base directory for this skill: {baseDirectory}");
        builder.AppendLine("Important: ALWAYS resolve absolute paths from this base directory when working with skills.");

        if (skill.AllowedTools.Count > 0)
        {
            builder.AppendLine($"Allowed tools: {string.Join(", ", skill.AllowedTools)}");
        }

        builder.AppendLine();
        builder.Append(body);
        return builder.ToString();
    }

    private static string TryGetSkillName(JsonElement arguments)
    {
        if (TryGetString(arguments, "skill", out var skill))
        {
            return skill;
        }

        return TryGetString(arguments, "skill_name", out var alternateSkillName)
            ? alternateSkillName
            : string.Empty;
    }

    private static bool TryGetString(JsonElement arguments, string propertyName, out string value)
    {
        value = string.Empty;
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string ExtractBody(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var match = FrontmatterRegex().Match(normalized);
        var body = match.Success ? normalized[match.Length..] : normalized;
        return body.Trim();
    }

    private static int GetScopePriority(string scope) =>
        string.Equals(scope, "project", StringComparison.OrdinalIgnoreCase) ? 2 :
        string.Equals(scope, "user", StringComparison.OrdinalIgnoreCase) ? 1 :
        0;

    [GeneratedRegex("^---\\n(?<yaml>[\\s\\S]*?)\\n---(?:\\n|$)", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}
