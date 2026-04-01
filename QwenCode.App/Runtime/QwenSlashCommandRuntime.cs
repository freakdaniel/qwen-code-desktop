using System.Text.RegularExpressions;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed partial class QwenSlashCommandRuntime(QwenCompatibilityService compatibilityService) : ISlashCommandRuntime
{
    public QwenResolvedCommand? TryResolve(
        SourceMirrorPaths paths,
        string prompt,
        string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var trimmedPrompt = prompt.Trim();
        if (!trimmedPrompt.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var firstSpace = trimmedPrompt.IndexOfAny([' ', '\r', '\n', '\t']);
        var commandToken = (firstSpace >= 0 ? trimmedPrompt[..firstSpace] : trimmedPrompt).TrimStart('/');
        var commandArguments = firstSpace >= 0 ? trimmedPrompt[(firstSpace + 1)..].Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(commandToken))
        {
            return null;
        }

        var compatibility = compatibilityService.Inspect(paths);
        var command = ResolveCommand(compatibility.Commands, commandToken);
        if (command is null)
        {
            return null;
        }

        var content = SafeReadAllText(command.Path);
        var body = ExtractBody(content);
        if (string.IsNullOrWhiteSpace(body))
        {
            body = command.Description;
        }

        var resolvedPrompt = RenderBody(body, commandArguments, workingDirectory);
        return new QwenResolvedCommand
        {
            Name = command.Name,
            Scope = command.Scope,
            SourcePath = command.Path,
            Description = command.Description,
            Arguments = commandArguments,
            ResolvedPrompt = resolvedPrompt
        };
    }

    private static QwenCommandSurface? ResolveCommand(
        IReadOnlyList<QwenCommandSurface> commands,
        string commandToken)
    {
        var normalized = commandToken.Replace('\\', '/');
        var exact = commands.FirstOrDefault(command =>
            string.Equals(command.Name, normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var byLeaf = commands
            .Where(command =>
                string.Equals(
                    command.Name[(command.Name.LastIndexOf('/') + 1)..],
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return byLeaf.Length == 1 ? byLeaf[0] : null;
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

    private static string ExtractBody(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var match = FrontmatterRegex().Match(normalized);
        return match.Success
            ? normalized[match.Length..].Trim()
            : normalized.Trim();
    }

    private static string RenderBody(string body, string arguments, string workingDirectory)
    {
        var rendered = body.Replace("{{args}}", arguments, StringComparison.Ordinal);
        rendered = rendered.Replace("{{cwd}}", workingDirectory.Replace('\\', '/'), StringComparison.Ordinal);

        if (!string.IsNullOrWhiteSpace(arguments) &&
            !body.Contains("{{args}}", StringComparison.Ordinal))
        {
            rendered = $"{rendered}\n\nArguments: {arguments}";
        }

        return rendered.Trim();
    }

    [GeneratedRegex("^---\\n[\\s\\S]*?\\n---(?:\\n|$)", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}
