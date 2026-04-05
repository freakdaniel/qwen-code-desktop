using System.Text.Json;
using QwenCode.App.Models;
using QwenCode.App.Sessions;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Assistant Prompt Assembler
/// </summary>
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
    private readonly ISessionService? _sessionService;

    /// <summary>
    /// Initializes a new instance of the AssistantPromptAssembler class
    /// </summary>
    /// <param name="projectSummaryService">The project summary service</param>
    /// <param name="sessionService">The session service</param>
    public AssistantPromptAssembler(IProjectSummaryService projectSummaryService, ISessionService? sessionService = null)
    {
        _projectSummaryService = projectSummaryService;
        _sessionService = sessionService;
    }

    /// <summary>
    /// Executes assemble async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="tokenLimits">The token limits</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to assistant prompt context</returns>
    public Task<AssistantPromptContext> AssembleAsync(
        AssistantTurnRequest request,
        ResolvedTokenLimits? tokenLimits = null,
        CancellationToken cancellationToken = default)
    {
        var allTranscriptMessages = ReadTranscriptMessages(request);
        var allContextFiles = ReadContextFiles(request.RuntimeProfile, request.WorkingDirectory);
        var promptBudget = ResolveBudget(tokenLimits, request);
        var transcriptMessages = TrimTranscriptMessages(allTranscriptMessages, promptBudget.TranscriptCharacterBudget);
        var contextFiles = TrimContextFiles(allContextFiles, promptBudget.ContextCharacterBudget);
        var projectSummary = _projectSummaryService.Read(request.RuntimeProfile);
        var sessionSummary = BuildSessionSummary(request, transcriptMessages, contextFiles, projectSummary);
        var trimmedTranscriptMessageCount = Math.Max(0, allTranscriptMessages.Count - transcriptMessages.Count);
        var trimmedContextFileCount = Math.Max(0, allContextFiles.Count - contextFiles.Count);

        return Task.FromResult(new AssistantPromptContext
        {
            Messages = transcriptMessages,
            ContextFiles = contextFiles,
            HistoryHighlights = transcriptMessages
                .TakeLast(Math.Min(MaxHistoryHighlights, transcriptMessages.Count))
                .Select(static item => $"{item.Role}: {Trim(item.Content, 120)}")
                .ToArray(),
            ProjectSummary = projectSummary,
            SessionSummary = $$"""
{{sessionSummary}}
Input token limit: {{promptBudget.InputTokenLimit}}
Approximate input character budget: {{promptBudget.TotalCharacterBudget}}
Prompt budget trimmed: {{(trimmedTranscriptMessageCount > 0 || trimmedContextFileCount > 0)}}
Trimmed transcript messages: {{trimmedTranscriptMessageCount}}
Trimmed context files: {{trimmedContextFileCount}}
""",
            WasBudgetTrimmed = trimmedTranscriptMessageCount > 0 || trimmedContextFileCount > 0,
            InputTokenLimit = promptBudget.InputTokenLimit,
            ApproximateInputCharacterBudget = promptBudget.TotalCharacterBudget,
            TrimmedTranscriptMessageCount = trimmedTranscriptMessageCount,
            TrimmedContextFileCount = trimmedContextFileCount
        });
    }

    private static PromptBudget ResolveBudget(ResolvedTokenLimits? tokenLimits, AssistantTurnRequest request)
    {
        var inputTokenLimit = tokenLimits?.InputTokenLimit ?? 131_072;
        var outputTokenLimit = tokenLimits?.OutputTokenLimit ?? 32_000;
        const int approximateCharactersPerToken = 4;
        const int reservedSystemAndMetadataTokens = 2_048;

        var availableInputTokens = Math.Max(
            1_024,
            inputTokenLimit - Math.Min(outputTokenLimit, inputTokenLimit / 2) - reservedSystemAndMetadataTokens);
        var totalCharacterBudget = availableInputTokens * approximateCharactersPerToken;
        var promptAndSummaryEstimate = Math.Max(1_024, request.Prompt.Length + 1_500);
        var remainderBudget = Math.Max(2_048, totalCharacterBudget - promptAndSummaryEstimate);

        return new PromptBudget(
            inputTokenLimit,
            totalCharacterBudget,
            Math.Max(768, (int)(remainderBudget * 0.55)),
            Math.Max(768, (int)(remainderBudget * 0.45)));
    }

    private IReadOnlyList<AssistantConversationMessage> ReadTranscriptMessages(AssistantTurnRequest request)
    {
        var sessionConversation = _sessionService?.LoadConversation(
            new WorkspacePaths { WorkspaceRoot = request.RuntimeProfile.ProjectRoot },
            request.SessionId);
        if (sessionConversation is not null && sessionConversation.ModelHistory.Count > 0)
        {
            return sessionConversation.ModelHistory
                .TakeLast(Math.Min(MaxTranscriptMessages, sessionConversation.ModelHistory.Count))
                .ToArray();
        }

        var transcriptPath = request.TranscriptPath;
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

    private static IReadOnlyList<AssistantConversationMessage> TrimTranscriptMessages(
        IReadOnlyList<AssistantConversationMessage> messages,
        int characterBudget)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var kept = new List<AssistantConversationMessage>();
        var consumed = 0;
        var perMessageBudget = Math.Max(240, characterBudget / Math.Max(1, Math.Min(messages.Count, MaxTranscriptMessages)));

        foreach (var message in messages.TakeLast(MaxTranscriptMessages).Reverse())
        {
            var trimmedContent = Trim(message.Content, perMessageBudget);
            var estimatedCost = message.Role.Length + trimmedContent.Length + 16;
            if (kept.Count > 0 && consumed + estimatedCost > characterBudget)
            {
                break;
            }

            kept.Add(new AssistantConversationMessage
            {
                Role = message.Role,
                Content = trimmedContent
            });
            consumed += estimatedCost;
        }

        kept.Reverse();
        return kept;
    }

    private static IReadOnlyList<string> TrimContextFiles(IReadOnlyList<string> contextFiles, int characterBudget)
    {
        if (contextFiles.Count == 0)
        {
            return [];
        }

        var kept = new List<string>();
        var consumed = 0;
        foreach (var contextFile in contextFiles)
        {
            var remainingBudget = Math.Max(256, characterBudget - consumed);
            if (remainingBudget <= 256 && kept.Count > 0)
            {
                break;
            }

            var trimmed = Trim(contextFile, remainingBudget);
            if (kept.Count > 0 && consumed + trimmed.Length > characterBudget)
            {
                break;
            }

            kept.Add(trimmed);
            consumed += trimmed.Length;
        }

        return kept;
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

    private sealed record PromptBudget(
        int InputTokenLimit,
        int TotalCharacterBudget,
        int TranscriptCharacterBudget,
        int ContextCharacterBudget);
}
