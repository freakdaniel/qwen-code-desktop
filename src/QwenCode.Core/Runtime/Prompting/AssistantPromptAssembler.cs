using QwenCode.Core.Models;
using QwenCode.Core.Mcp;
using QwenCode.Core.Prompts;
using QwenCode.Core.Sessions;
using QwenCode.Core.Tools;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Assistant Prompt Assembler
/// </summary>
public sealed class AssistantPromptAssembler : IAssistantPromptAssembler
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly System.Text.RegularExpressions.Regex ApprovalPlaceholderRegex =
        new("tool '([^']+)' is waiting for approval(?:[^.]*)\\.?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private const int MaxTranscriptMessages = 16;
    private const int MaxContextFiles = 12;
    private const int MaxContextCharacters = 6000;
    private const int MaxHistoryHighlights = 8;
    private const int MaxImportDepth = 6;
    private const int MaxSessionMemoryCharacters = 4000;
    private const string ChatCompressionStatus = "chat-compression";
    private static readonly string[] DefaultContextFileNames = ["QWEN.md", "AGENTS.md"];
    private readonly IProjectSummaryService _projectSummaryService;
    private readonly ISessionService? _sessionService;
    private readonly IMcpConnectionManager? _mcpConnectionManager;
    private readonly IPromptRegistryService? _promptRegistryService;

    /// <summary>
    /// Initializes a new instance of the AssistantPromptAssembler class
    /// </summary>
    /// <param name="projectSummaryService">The project summary service</param>
    /// <param name="sessionService">The session service</param>
    /// <param name="mcpConnectionManager">The mcp connection manager</param>
    /// <param name="promptRegistryService">The prompt registry service</param>
    public AssistantPromptAssembler(
        IProjectSummaryService projectSummaryService,
        ISessionService? sessionService = null,
        IMcpConnectionManager? mcpConnectionManager = null,
        IPromptRegistryService? promptRegistryService = null)
    {
        _projectSummaryService = projectSummaryService;
        _sessionService = sessionService;
        _mcpConnectionManager = mcpConnectionManager;
        _promptRegistryService = promptRegistryService;
    }

    /// <summary>
    /// Executes assemble async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="tokenLimits">The token limits</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to assistant prompt context</returns>
    public async Task<AssistantPromptContext> AssembleAsync(
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
        var environmentSummary = BuildEnvironmentSummary(request);
        var trimmedTranscriptMessageCount = Math.Max(0, allTranscriptMessages.Count - transcriptMessages.Count);
        var trimmedContextFileCount = Math.Max(0, allContextFiles.Count - contextFiles.Count);
        var sessionMemorySummary = BuildSessionMemorySummary(request);
        var sessionGuidanceSummary = BuildSessionGuidanceSummary(
            request,
            transcriptMessages,
            contextFiles,
            projectSummary,
            promptBudget,
            sessionMemorySummary,
            trimmedTranscriptMessageCount,
            trimmedContextFileCount);
        var userInstructionSummary = BuildInstructionSummary(request.RuntimeProfile, request.WorkingDirectory, scope: "global");
        var workspaceInstructionSummary = BuildInstructionSummary(request.RuntimeProfile, request.WorkingDirectory, scope: "workspace");
        var durableMemorySummary = BuildDurableMemorySummary(request.RuntimeProfile);
        var mcpServerSummary = BuildMcpServerSummary(request);
        var mcpPromptRegistrySummary = await BuildMcpPromptRegistrySummaryAsync(request, cancellationToken);
        var scratchpadSummary = BuildScratchpadSummary(request);
        var languageSummary = BuildLanguageSummary(request);
        var outputStyleSummary = BuildOutputStyleSummary(request);

        return new AssistantPromptContext
        {
            Messages = transcriptMessages,
            ContextFiles = contextFiles,
            HistoryHighlights = transcriptMessages
                .TakeLast(Math.Min(MaxHistoryHighlights, transcriptMessages.Count))
                .Select(static item => $"{item.Role}: {Trim(item.Content, 120)}")
                .ToArray(),
            ProjectSummary = projectSummary,
            EnvironmentSummary = environmentSummary,
            SessionGuidanceSummary = sessionGuidanceSummary,
            DurableMemorySummary = durableMemorySummary,
            SessionMemorySummary = sessionMemorySummary,
            UserInstructionSummary = userInstructionSummary,
            WorkspaceInstructionSummary = workspaceInstructionSummary,
            McpServerSummary = mcpServerSummary,
            McpPromptRegistrySummary = mcpPromptRegistrySummary,
            ScratchpadSummary = scratchpadSummary,
            LanguageSummary = languageSummary,
            OutputStyleSummary = outputStyleSummary,
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
        };
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
                var status = TryGetString(root, "status") ?? string.Empty;
                var resolutionStatus = TryGetString(root, "resolutionStatus") ?? string.Empty;
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

                if (type == "assistant" && ApprovalPlaceholderRegex.IsMatch(content.Trim()))
                {
                    continue;
                }

                if (type == "tool" &&
                    string.Equals(status, "approval-required", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(resolutionStatus))
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

    private static string BuildSessionMemorySummary(AssistantTurnRequest request)
    {
        var checkpoint = ReadLatestCompressionCheckpoint(request.TranscriptPath);
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            return string.Empty;
        }

        return $$"""
Latest chat compression checkpoint for this session:
{{checkpoint}}
""";
    }

    private static string? ReadLatestCompressionCheckpoint(string transcriptPath)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath) || !File.Exists(transcriptPath))
        {
            return null;
        }

        string? latestCheckpoint = null;
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
                if (!string.Equals(TryGetString(root, "type"), "system", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(TryGetString(root, "status"), ChatCompressionStatus, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var messageText = TryGetString(root, "messageText");
                if (string.IsNullOrWhiteSpace(messageText))
                {
                    continue;
                }

                var timestamp = TryGetString(root, "timestamp");
                var trimmedMessage = Trim(messageText.Trim(), MaxSessionMemoryCharacters);
                latestCheckpoint = string.IsNullOrWhiteSpace(timestamp)
                    ? trimmedMessage
                    : $"Recorded at {timestamp}: {trimmedMessage}";
            }
            catch
            {
                // Keep session memory assembly resilient to malformed transcript lines.
            }
        }

        return latestCheckpoint;
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
Prompt mode: {{request.PromptMode}}
Transcript messages loaded: {{transcriptMessages.Count}}
Context files loaded: {{contextFiles.Count}}
Context file names: {{string.Join(", ", request.RuntimeProfile.ContextFileNames)}}
{{projectSummaryStatus}}
{{commandSummary}}
{{toolSummary}}
Approval resolution: {{request.IsApprovalResolution}}
""";
    }

    private static string BuildEnvironmentSummary(AssistantTurnRequest request)
    {
        var runtimeProfile = request.RuntimeProfile;
        var trustSummary = runtimeProfile.FolderTrustEnabled
            ? $"Workspace trusted: {runtimeProfile.IsWorkspaceTrusted} ({runtimeProfile.WorkspaceTrustSource})."
            : "Workspace trust: disabled.";
        var shellProgram = DetectShellProgram();
        var platform = DetectPlatform();
        var contextFileNames = runtimeProfile.ContextFileNames.Count == 0
            ? "(none)"
            : string.Join(", ", runtimeProfile.ContextFileNames);
        var modelSummary = string.IsNullOrWhiteSpace(runtimeProfile.ModelName)
            ? "Model preference: not specified in runtime profile."
            : $"Model preference: {runtimeProfile.ModelName}.";
        var localeSummary = string.IsNullOrWhiteSpace(runtimeProfile.CurrentLocale) &&
                            string.IsNullOrWhiteSpace(runtimeProfile.CurrentLanguage)
            ? "Locale preference: not specified in runtime profile."
            : $"Locale preference: {runtimeProfile.CurrentLocale} / {runtimeProfile.CurrentLanguage}.";
        var compressionSummary = runtimeProfile.ChatCompression is null
            ? "Chat compression: not configured."
            : $"Chat compression threshold: {runtimeProfile.ChatCompression.ContextPercentageThreshold?.ToString("0.##") ?? "(unspecified)"} of context.";

        return $$"""
Workspace root: {{runtimeProfile.ProjectRoot}}
Working directory: {{request.WorkingDirectory}}
Git branch: {{(string.IsNullOrWhiteSpace(request.GitBranch) ? "(none)" : request.GitBranch)}}
Current local date: {{DateTimeOffset.Now:yyyy-MM-dd}}
Current local time zone: {{TimeZoneInfo.Local.Id}}
Platform: {{platform}}
Shell: {{shellProgram}}
Runtime source: {{runtimeProfile.RuntimeSource}}
Approval mode: {{runtimeProfile.ApprovalProfile.DefaultMode}}
Confirm shell commands: {{runtimeProfile.ApprovalProfile.ConfirmShellCommands}}
Confirm file edits: {{runtimeProfile.ApprovalProfile.ConfirmFileEdits}}
Runtime base directory: {{runtimeProfile.RuntimeBaseDirectory}}
Project data directory: {{runtimeProfile.ProjectDataDirectory}}
Chats directory: {{runtimeProfile.ChatsDirectory}}
History directory: {{runtimeProfile.HistoryDirectory}}
Checkpointing enabled: {{runtimeProfile.Checkpointing}}
{{compressionSummary}}
{{modelSummary}}
{{localeSummary}}
{{trustSummary}}
Declared context file names: {{contextFileNames}}
""";
    }

    private static string BuildSessionGuidanceSummary(
        AssistantTurnRequest request,
        IReadOnlyList<AssistantConversationMessage> transcriptMessages,
        IReadOnlyList<string> contextFiles,
        ProjectSummarySnapshot? projectSummary,
        PromptBudget promptBudget,
        string sessionMemorySummary,
        int trimmedTranscriptMessageCount,
        int trimmedContextFileCount)
    {
        var commandSummary = request.CommandInvocation is null
            ? "No slash command resolved in this turn."
            : $"Slash command '/{request.CommandInvocation.Command.Name}' finished with status '{request.CommandInvocation.Status}'.";
        var toolSummary = request.ToolExecution.Status == "not-requested"
            ? "No native tool result exists yet for this provider round."
            : $"Latest native tool result: '{request.ToolExecution.ToolName}' ({request.ToolExecution.Status}).";
        var projectSummaryStatus = projectSummary is null
            ? "No project summary is available."
            : $"Project summary available from '{projectSummary.FilePath}' with {projectSummary.PendingTasks.Count} pending task(s).";
        var sessionMemoryStatus = string.IsNullOrWhiteSpace(sessionMemorySummary)
            ? "No session memory checkpoint is available."
            : "Session memory checkpoint retained in the system prompt.";

        return $$"""
Transcript messages retained for this turn: {{transcriptMessages.Count}}
Workspace context files retained for this turn: {{contextFiles.Count}}
{{sessionMemoryStatus}}
{{projectSummaryStatus}}
{{commandSummary}}
{{toolSummary}}
Budget trimmed transcript messages: {{trimmedTranscriptMessageCount}}
Budget trimmed context files: {{trimmedContextFileCount}}
Approximate input character budget: {{promptBudget.TotalCharacterBudget}}
Approval resolution turn: {{request.IsApprovalResolution}}
Prompt mode: {{request.PromptMode}}
""";
    }

    private static string BuildLanguageSummary(AssistantTurnRequest request)
    {
        var runtimeProfile = request.RuntimeProfile;
        var locale = string.IsNullOrWhiteSpace(runtimeProfile.CurrentLocale)
            ? RuntimeLocaleCatalog.DetectLocale()
            : RuntimeLocaleCatalog.NormalizeLocale(runtimeProfile.CurrentLocale);
        var language = string.IsNullOrWhiteSpace(runtimeProfile.CurrentLanguage)
            ? RuntimeLocaleCatalog.ResolveLanguageName(locale)
            : runtimeProfile.CurrentLanguage;

        return $$"""
Preferred locale: {{locale}}
Preferred language: {{language}}
Default expectation: reply in the user's language when it is clear from the conversation; otherwise start from the preferred language.
""";
    }

    private static string BuildOutputStyleSummary(AssistantTurnRequest request)
    {
        var modeSpecificExpectation = request.PromptMode switch
        {
            AssistantPromptMode.Plan =>
                "Prefer a structured plan with ordered steps, dependencies, and risks. Avoid presenting unexecuted work as completed.",
            AssistantPromptMode.FollowupSuggestion =>
                "Return one short user-like follow-up suggestion only, with no explanation or extra framing.",
            AssistantPromptMode.Subagent =>
                "Return a concise parent-facing execution summary with evidence, blockers, and changed files.",
            AssistantPromptMode.ArenaCompetitor =>
                "Return a concise competitive wrap-up that explains changes, risks, and why the approach is strong.",
            _ =>
                "Prefer concise, action-oriented answers that report concrete results before optional detail."
        };

        var planModeExpectation = string.Equals(request.RuntimeProfile.ApprovalProfile.DefaultMode, "plan", StringComparison.OrdinalIgnoreCase)
            ? "Plan-style approval mode is active, so prioritize read-only investigation and planning unless the user explicitly exits that mode."
            : "Use normal execution behavior for the active approval mode.";

        return $$"""
Mode-specific expectation: {{modeSpecificExpectation}}
Approval-aware expectation: {{planModeExpectation}}
Verification expectation: state clearly what was verified, what was inferred, and what remains unverified.
Math formatting expectation: when including formulas, use KaTeX-safe LaTeX only. Inline math must use `$...$`, display math must use `$$...$$`, percent signs inside math must be escaped as `\%`, never use `%` comments inside math, and prose inside formulas should use `\text{...}`. Do not emit formulas as only bold/plain text, Unicode superscripts, or raw TeX without math delimiters. If you are not confident the formula is valid KaTeX, explain it in plain text instead of emitting broken LaTeX.
""";
    }

    private string BuildMcpServerSummary(AssistantTurnRequest request)
    {
        if (_mcpConnectionManager is null)
        {
            return string.Empty;
        }

        try
        {
            var servers = _mcpConnectionManager
                .ListServersWithStatus(new WorkspacePaths { WorkspaceRoot = request.RuntimeProfile.ProjectRoot })
                .Where(static server => string.Equals(server.Status, "connected", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static server => server.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (servers.Length == 0)
            {
                return string.Empty;
            }

            return string.Join(
                Environment.NewLine,
                servers.Select(static server =>
                {
                    var instructionSuffix = string.IsNullOrWhiteSpace(server.Instructions)
                        ? string.Empty
                        : $" Instructions: {server.Instructions.Trim()}";
                    var capabilityHints = new List<string>();
                    if (server.SupportsPrompts)
                    {
                        capabilityHints.Add("Use `mcp-client` with `server_name` + `prompt_name` to inspect or invoke MCP prompts.");
                    }

                    if (server.SupportsResources)
                    {
                        capabilityHints.Add("Use `mcp-client` with `server_name` + `uri` to read MCP resources when a resource URI is relevant.");
                    }

                    if (server.DiscoveredToolsCount > 0)
                    {
                        capabilityHints.Add("Use `mcp-tool` for concrete server-exposed actions after identifying the correct MCP tool and arguments.");
                    }

                    var capabilitySuffix = capabilityHints.Count == 0
                        ? string.Empty
                        : $" Guidance: {string.Join(" ", capabilityHints)}";
                    return $"- {server.Name} ({server.Scope}, {server.Transport}): " +
                           $"{server.DiscoveredToolsCount} tool(s), " +
                           $"{server.DiscoveredPromptsCount} prompt(s), " +
                           $"resources {(server.SupportsResources ? "available" : "unavailable")}, " +
                           $"prompts {(server.SupportsPrompts ? "available" : "unavailable")}, " +
                           $"trust={server.Trust}, " +
                           $"status={server.Status}." +
                           instructionSuffix +
                           capabilitySuffix;
                }));
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> BuildMcpPromptRegistrySummaryAsync(
        AssistantTurnRequest request,
        CancellationToken cancellationToken)
    {
        if (_promptRegistryService is null)
        {
            return string.Empty;
        }

        try
        {
            var snapshot = await _promptRegistryService.GetSnapshotAsync(
                new WorkspacePaths
                {
                    WorkspaceRoot = request.RuntimeProfile.ProjectRoot
                },
                new GetPromptRegistryRequest(),
                cancellationToken);
            if (snapshot.TotalCount == 0 || snapshot.Prompts.Count == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>
            {
                $"Discovered MCP prompts: {snapshot.TotalCount} across {snapshot.ServerCount} server(s).",
                "Prefer these named MCP prompts when one directly matches the task instead of guessing lower-level MCP tool calls."
            };

            foreach (var prompt in snapshot.Prompts.Take(8))
            {
                var argumentNames = ExtractPromptArgumentNames(prompt.ArgumentsJson);
                var argumentSuffix = argumentNames.Count == 0
                    ? "Args: none."
                    : $"Args: {string.Join(", ", argumentNames)}.";
                var descriptionSuffix = string.IsNullOrWhiteSpace(prompt.Description)
                    ? string.Empty
                    : $" {Trim(prompt.Description.Trim(), 120)}";
                lines.Add($"- `{prompt.QualifiedName}`.{descriptionSuffix} {argumentSuffix}".TrimEnd());
            }

            if (snapshot.Prompts.Count > 8)
            {
                lines.Add($"- {snapshot.Prompts.Count - 8} additional MCP prompt(s) omitted from this summary for brevity.");
            }

            return string.Join(Environment.NewLine, lines);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<string> ExtractPromptArgumentNames(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return document.RootElement
                .EnumerateArray()
                .Select(static item =>
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        return string.Empty;
                    }

                    if (item.TryGetProperty("name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String)
                    {
                        return nameProperty.GetString() ?? string.Empty;
                    }

                    return string.Empty;
                })
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string BuildScratchpadSummary(AssistantTurnRequest request)
    {
        var sanitizedSessionId = SanitizePathSegment(request.SessionId);
        var scratchpadDirectory = Path.Combine(
            request.RuntimeProfile.RuntimeBaseDirectory,
            "tmp",
            "scratchpad",
            sanitizedSessionId);

        return $$"""
Use `{{scratchpadDirectory}}` for temporary files, intermediate outputs, and helper scripts that should not become part of the user's project.
- Prefer this directory over cluttering the repository with transient artifacts.
- Write into the project itself only when the result is meant to persist as part of the user's workspace.
""";
    }

    private static string BuildDurableMemorySummary(QwenRuntimeProfile runtimeProfile)
    {
        var sections = new List<string>();
        sections.AddRange(BuildDurableMemoryScopeSections("Global", runtimeProfile.GlobalQwenDirectory, runtimeProfile.ContextFileNames));

        if (runtimeProfile.IsWorkspaceTrusted)
        {
            sections.AddRange(BuildDurableMemoryScopeSections("Project", runtimeProfile.ProjectRoot, runtimeProfile.ContextFileNames));
        }

        return sections.Count == 0
            ? string.Empty
            : string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static string BuildInstructionSummary(
        QwenRuntimeProfile runtimeProfile,
        string workingDirectory,
        string scope)
    {
        var contextFilePaths = DiscoverContextFilePaths(runtimeProfile, workingDirectory);
        if (contextFilePaths.Count == 0)
        {
            return string.Empty;
        }

        var sections = new List<string>();
        foreach (var path in contextFilePaths)
        {
            var isGlobalInstruction = IsPathWithin(path, runtimeProfile.GlobalQwenDirectory);
            var isWorkspaceInstruction = IsPathWithin(path, runtimeProfile.ProjectRoot);
            if (string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase) && !isGlobalInstruction)
            {
                continue;
            }

            if (string.Equals(scope, "workspace", StringComparison.OrdinalIgnoreCase) && !isWorkspaceInstruction)
            {
                continue;
            }

            try
            {
                var content = ReadContextFileWithImports(runtimeProfile, path, new HashSet<string>(PathComparer), 0);
                var facts = ExtractInstructionFacts(content);
                if (facts.Count == 0)
                {
                    continue;
                }

                sections.Add(
                    $$"""
From {{GetDisplayPath(runtimeProfile.ProjectRoot, runtimeProfile.GlobalQwenDirectory, path)}}:
{{string.Join(Environment.NewLine, facts.Select(static fact => $"- {fact}"))}}
""");
            }
            catch
            {
                // Ignore unreadable instruction files while building prompt summaries.
            }
        }

        return sections.Count == 0
            ? string.Empty
            : string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static IReadOnlyList<string> BuildDurableMemoryScopeSections(
        string scopeLabel,
        string rootPath,
        IReadOnlyList<string> contextFileNames)
    {
        var sections = new List<string>();
        foreach (var fileName in contextFileNames.Where(static name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(rootPath, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(path);
                var facts = ExtractDurableMemoryFacts(content);
                if (facts.Count == 0)
                {
                    continue;
                }

                sections.Add(
                    $$"""
{{scopeLabel}} durable memory ({{Path.GetFileName(path)}}):
{{string.Join(Environment.NewLine, facts.Select(static fact => $"- {fact}"))}}
""");
            }
            catch
            {
                // Ignore unreadable memory files while building prompt summaries.
            }
        }

        return sections;
    }

    private static IReadOnlyList<string> ExtractDurableMemoryFacts(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var headerIndex = content.IndexOf(MemoryStore.MemorySectionHeader, StringComparison.Ordinal);
        if (headerIndex < 0)
        {
            return [];
        }

        var startIndex = headerIndex + MemoryStore.MemorySectionHeader.Length;
        var endIndex = content.IndexOf($"{Environment.NewLine}## ", startIndex, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            endIndex = content.Length;
        }

        var section = content[startIndex..endIndex];
        return section
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("-", StringComparison.Ordinal))
            .Select(static line => line.TrimStart('-').Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractInstructionFacts(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var sanitized = RemoveDurableMemorySection(content);
        var facts = new List<string>();
        foreach (var rawLine in sanitized.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) ||
                string.Equals(line, "---", StringComparison.Ordinal) ||
                string.Equals(line, MemoryStore.MemorySectionHeader, StringComparison.Ordinal) ||
                line.StartsWith("@", StringComparison.Ordinal) ||
                line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var normalized = TrimInstructionPrefix(line);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            facts.Add(normalized);
        }

        return facts
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static string RemoveDurableMemorySection(string content)
    {
        var headerIndex = content.IndexOf(MemoryStore.MemorySectionHeader, StringComparison.Ordinal);
        if (headerIndex < 0)
        {
            return content;
        }

        var endIndex = content.IndexOf($"{Environment.NewLine}## ", headerIndex + MemoryStore.MemorySectionHeader.Length, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            endIndex = content.Length;
        }

        return string.Concat(content.AsSpan(0, headerIndex), content.AsSpan(endIndex));
    }

    private static string TrimInstructionPrefix(string line)
    {
        var normalized = line.Trim();
        while (normalized.StartsWith("-", StringComparison.Ordinal) ||
               normalized.StartsWith("*", StringComparison.Ordinal))
        {
            normalized = normalized[1..].TrimStart();
        }

        var separatorIndex = normalized.IndexOf(". ", StringComparison.Ordinal);
        if (separatorIndex > 0 &&
            normalized[..separatorIndex].All(char.IsDigit))
        {
            normalized = normalized[(separatorIndex + 2)..].TrimStart();
        }

        return normalized;
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? TryExtractMessageText(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) ||
            message.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(content.GetString()))
        {
            return content.GetString();
        }

        if (!message.TryGetProperty("parts", out var parts) ||
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

    private static bool IsPathWithin(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root);
        return fullPath.StartsWith(
            fullRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "session";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "session" : sanitized;
    }

    private static string DetectShellProgram()
    {
        var shell = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrWhiteSpace(shell))
        {
            return Path.GetFileName(shell);
        }

        var comSpec = Environment.GetEnvironmentVariable("ComSpec");
        if (!string.IsNullOrWhiteSpace(comSpec))
        {
            return Path.GetFileName(comSpec);
        }

        return "unknown";
    }

    private static string DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        return RuntimeInformation.OSDescription;
    }

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : $"{value[..maxLength]}...";

    private sealed record PromptBudget(
        int InputTokenLimit,
        int TotalCharacterBudget,
        int TranscriptCharacterBudget,
        int ContextCharacterBudget);
}
