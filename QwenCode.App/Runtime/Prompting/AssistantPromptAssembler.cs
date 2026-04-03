using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class AssistantPromptAssembler : IAssistantPromptAssembler
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private const int MaxTranscriptMessages = 16;
    private const int MaxContextFiles = 12;
    private const int MaxContextCharacters = 6000;
    private const int MaxHistoryHighlights = 8;
    private const int MaxImportDepth = 6;
    private static readonly string[] DefaultContextFileNames = ["QWEN.md", "AGENTS.md"];
    private readonly IProjectSummaryService _projectSummaryService;

    public AssistantPromptAssembler(IProjectSummaryService projectSummaryService)
    {
        _projectSummaryService = projectSummaryService;
    }

    public Task<AssistantPromptContext> AssembleAsync(
        AssistantTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var transcriptMessages = ReadTranscriptMessages(request.TranscriptPath);
        var contextFiles = ReadContextFiles(request.RuntimeProfile, request.WorkingDirectory);
        var projectSummary = _projectSummaryService.Read(request.RuntimeProfile);
        var sessionSummary = BuildSessionSummary(request, transcriptMessages, contextFiles, projectSummary);

        return Task.FromResult(new AssistantPromptContext
        {
            Messages = transcriptMessages,
            ContextFiles = contextFiles,
            HistoryHighlights = transcriptMessages
                .TakeLast(Math.Min(MaxHistoryHighlights, transcriptMessages.Count))
                .Select(static item => $"{item.Role}: {Trim(item.Content, 120)}")
                .ToArray(),
            ProjectSummary = projectSummary,
            SessionSummary = sessionSummary
        });
    }

    private static IReadOnlyList<AssistantConversationMessage> ReadTranscriptMessages(string transcriptPath)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath) || !File.Exists(transcriptPath))
        {
            return [];
        }

        var messages = new List<AssistantConversationMessage>();
        foreach (var line in File.ReadLines(transcriptPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var type = TryGetString(root, "type") ?? string.Empty;
                var content = type switch
                {
                    "user" or "assistant" => TryExtractMessageText(root),
                    "command" => TryGetString(root, "resolvedPrompt") ?? TryGetString(root, "output"),
                    "tool" => TryGetString(root, "output") ?? TryGetString(root, "errorMessage") ?? TryGetString(root, "approvalState"),
                    "system" => TryGetString(root, "messageText") ?? TryGetString(root, "status"),
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                messages.Add(new AssistantConversationMessage
                {
                    Role = type switch
                    {
                        "user" => "user",
                        "assistant" => "assistant",
                        "command" => "system",
                        "tool" => "system",
                        _ => "system"
                    },
                    Content = content.Trim()
                });
            }
            catch
            {
                // Keep context assembly resilient to malformed transcript lines.
            }
        }

        return messages.TakeLast(MaxTranscriptMessages).ToArray();
    }

    private static IReadOnlyList<string> ReadContextFiles(QwenRuntimeProfile runtimeProfile, string workingDirectory)
    {
        var paths = DiscoverContextFilePaths(runtimeProfile, workingDirectory);
        var results = new List<string>();
        foreach (var path in paths.Take(MaxContextFiles))
        {
            try
            {
                var content = ReadContextFileWithImports(runtimeProfile, path, new HashSet<string>(PathComparer), 0);
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                var displayPath = GetDisplayPath(runtimeProfile.ProjectRoot, runtimeProfile.GlobalQwenDirectory, path);
                results.Add(
                    $"--- Context from: {displayPath} ---{Environment.NewLine}" +
                    $"{Trim(content.Trim(), MaxContextCharacters)}{Environment.NewLine}" +
                    $"--- End of Context from: {displayPath} ---");
            }
            catch
            {
                // Ignore unreadable context files.
            }
        }

        return results;
    }

    private static IReadOnlyList<string> DiscoverContextFilePaths(QwenRuntimeProfile runtimeProfile, string workingDirectory)
    {
        var contextFileNames = runtimeProfile.ContextFileNames.Count > 0
            ? runtimeProfile.ContextFileNames
            : DefaultContextFileNames;
        var discovered = new List<string>();
        var seen = new HashSet<string>(PathComparer);

        foreach (var fileName in contextFileNames)
        {
            var globalPath = Path.Combine(runtimeProfile.GlobalQwenDirectory, fileName);
            TryAdd(globalPath);
        }

        if (runtimeProfile.IsWorkspaceTrusted)
        {
            foreach (var directory in EnumerateWorkspaceDirectories(runtimeProfile.ProjectRoot, workingDirectory))
            {
                foreach (var fileName in contextFileNames)
                {
                    TryAdd(Path.Combine(directory, fileName));
                }
            }
        }

        return discovered;

        void TryAdd(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (seen.Add(fullPath))
            {
                discovered.Add(fullPath);
            }
        }
    }

    private static IReadOnlyList<string> EnumerateWorkspaceDirectories(string projectRoot, string workingDirectory)
    {
        var normalizedProjectRoot = Path.GetFullPath(projectRoot);
        var normalizedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? normalizedProjectRoot
            : Path.GetFullPath(workingDirectory);

        if (!normalizedWorkingDirectory.StartsWith(
                normalizedProjectRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            normalizedWorkingDirectory = normalizedProjectRoot;
        }

        var directories = new Stack<string>();
        var current = normalizedWorkingDirectory;
        while (true)
        {
            directories.Push(current);
            if (PathComparer.Equals(current, normalizedProjectRoot))
            {
                break;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
            {
                break;
            }

            current = parent;
        }

        return directories.ToArray();
    }

    private static string ReadContextFileWithImports(
        QwenRuntimeProfile runtimeProfile,
        string path,
        HashSet<string> visited,
        int depth)
    {
        var fullPath = Path.GetFullPath(path);
        if (!visited.Add(fullPath))
        {
            return string.Empty;
        }

        if (depth >= MaxImportDepth)
        {
            return File.ReadAllText(fullPath);
        }

        var lines = File.ReadAllLines(fullPath);
        var processedLines = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('@') &&
                trimmed.Length > 1 &&
                !trimmed.Contains(' ') &&
                !trimmed.Contains('\t'))
            {
                var importPath = trimmed[1..];
                var resolvedPath = Path.IsPathRooted(importPath)
                    ? importPath
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, importPath));
                if (!runtimeProfile.IsWorkspaceTrusted && IsWithinWorkspace(resolvedPath, runtimeProfile.ProjectRoot))
                {
                    continue;
                }

                if (File.Exists(resolvedPath))
                {
                    var importedContent = ReadContextFileWithImports(runtimeProfile, resolvedPath, visited, depth + 1);
                    if (!string.IsNullOrWhiteSpace(importedContent))
                    {
                        processedLines.Add(importedContent);
                    }

                    continue;
                }
            }

            processedLines.Add(line);
        }

        return string.Join(Environment.NewLine, processedLines);
    }

    private static string GetDisplayPath(string projectRoot, string globalQwenDirectory, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var normalizedProjectRoot = Path.GetFullPath(projectRoot);
        var normalizedGlobalDirectory = Path.GetFullPath(globalQwenDirectory);

        if (fullPath.StartsWith(
                normalizedProjectRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return Path.GetRelativePath(normalizedProjectRoot, fullPath);
        }

        if (fullPath.StartsWith(
                normalizedGlobalDirectory,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return Path.Combine("~", ".qwen", Path.GetRelativePath(normalizedGlobalDirectory, fullPath));
        }

        return fullPath;
    }

    private static string BuildSessionSummary(
        AssistantTurnRequest request,
        IReadOnlyList<AssistantConversationMessage> transcriptMessages,
        IReadOnlyList<string> contextFiles,
        ProjectSummarySnapshot? projectSummary)
    {
        var commandSummary = request.CommandInvocation is null
            ? "No command resolved."
            : $"Command '/{request.CommandInvocation.Command.Name}' finished with status '{request.CommandInvocation.Status}'.";
        var toolSummary = request.ToolExecution.Status == "not-requested"
            ? "No tool requested."
            : $"Tool '{request.ToolExecution.ToolName}' finished with status '{request.ToolExecution.Status}'.";
        var projectSummaryStatus = projectSummary is null
            ? "Project summary: not found."
            : $"Project summary: loaded from '{projectSummary.FilePath}' ({projectSummary.TimeAgo}, pending tasks: {projectSummary.PendingTasks.Count}).";

        return $$"""
Session: {{request.SessionId}}
Workspace: {{request.RuntimeProfile.ProjectRoot}}
Working directory: {{request.WorkingDirectory}}
Git branch: {{(string.IsNullOrWhiteSpace(request.GitBranch) ? "(none)" : request.GitBranch)}}
Approval mode: {{request.RuntimeProfile.ApprovalProfile.DefaultMode}}
Transcript messages loaded: {{transcriptMessages.Count}}
Context files loaded: {{contextFiles.Count}}
Context file names: {{string.Join(", ", request.RuntimeProfile.ContextFileNames)}}
{{projectSummaryStatus}}
{{commandSummary}}
{{toolSummary}}
Approval resolution: {{request.IsApprovalResolution}}
""";
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? TryExtractMessageText(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) ||
            message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object &&
                part.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(text.GetString()))
            {
                return text.GetString();
            }
        }

        return null;
    }

    private static bool IsWithinWorkspace(string path, string workspaceRoot)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(workspaceRoot);
        return fullPath.StartsWith(
            fullRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..maxLength]}...";
}
