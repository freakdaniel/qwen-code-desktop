using System.Text.Json;
using QwenCode.App.Models;
using QwenCode.App.Compatibility;
using QwenCode.App.Runtime;

namespace QwenCode.App.Sessions;

public sealed class DesktopSessionCatalogService(
    QwenRuntimeProfileService runtimeProfileService,
    IChatRecordingService chatRecordingService) : ITranscriptStore, ISessionService
{
    private const int DefaultDetailEntryLimit = 120;
    private const int MaximumDetailEntryLimit = 240;
    private const int MaximumBodyLength = 12_000;
    private const int MaximumArgumentsLength = 4_000;
    private const int MaximumSourcePathLength = 1_024;
    private const int MaximumChangedFileCount = 128;

    public IReadOnlyList<SessionPreview> ListSessions(WorkspacePaths paths, int limit = 24)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        if (!Directory.Exists(runtimeProfile.ChatsDirectory))
        {
            return [];
        }

        var sessions = Directory.EnumerateFiles(runtimeProfile.ChatsDirectory, "*.jsonl")
            .Select(static path => new FileInfo(path))
            .Where(static file => file.Exists)
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .Take(limit)
            .Select(file => TryReadSession(file, runtimeProfile, chatRecordingService))
            .OfType<SessionPreview>()
            .ToArray();

        return sessions;
    }

    public DesktopSessionDetail? GetSession(WorkspacePaths paths, GetDesktopSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            return null;
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, $"{request.SessionId}.jsonl");
        if (!File.Exists(transcriptPath))
        {
            return null;
        }

        var preview = ListSessions(paths, 128).FirstOrDefault(item =>
            string.Equals(item.SessionId, request.SessionId, StringComparison.Ordinal));
        if (preview is null)
        {
            return null;
        }

        var entries = new List<DesktopSessionEntry>();
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
                entries.Add(ParseEntry(root));
            }
            catch
            {
                // Skip malformed entries so the session remains readable.
            }
        }

        var requestedLimit = request.Limit.GetValueOrDefault(DefaultDetailEntryLimit);
        var windowSize = Math.Clamp(requestedLimit, 1, MaximumDetailEntryLimit);
        var maxOffset = Math.Max(0, entries.Count - windowSize);
        var windowOffset = request.Offset.HasValue
            ? Math.Clamp(request.Offset.Value, 0, maxOffset)
            : maxOffset;
        var entryWindow = entries
            .Skip(windowOffset)
            .Take(windowSize)
            .Select(SanitizeEntryForDesktopProjection)
            .ToArray();

        return new DesktopSessionDetail
        {
            Session = preview,
            TranscriptPath = transcriptPath,
            EntryCount = entries.Count,
            WindowOffset = windowOffset,
            WindowSize = entryWindow.Length,
            HasOlderEntries = windowOffset > 0,
            HasNewerEntries = windowOffset + entryWindow.Length < entries.Count,
            Summary = DesktopSessionActivitySummaryBuilder.Build(entries),
            Entries = entryWindow
        };
    }

    public bool SessionExists(WorkspacePaths paths, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        return File.Exists(Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl"));
    }

    public SessionPreview? LoadLastSession(WorkspacePaths paths) =>
        ListSessions(paths, 1).FirstOrDefault();

    public SessionConversationRecord? LoadConversation(WorkspacePaths paths, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl");
        if (!File.Exists(transcriptPath))
        {
            return null;
        }

        var parsedEntries = new List<ParsedTranscriptEntry>();
        foreach (var line in File.ReadLines(transcriptPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                parsedEntries.Add(ParseTranscriptEntry(document.RootElement));
            }
            catch
            {
                // Keep conversation loading resilient to malformed transcript lines.
            }
        }

        if (parsedEntries.Count == 0)
        {
            return null;
        }

        var latestCompressionIndex = parsedEntries.FindLastIndex(static entry =>
            string.Equals(entry.Type, "system", StringComparison.Ordinal) &&
            string.Equals(entry.Status, "chat-compression", StringComparison.OrdinalIgnoreCase));

        var modelHistory = new List<AssistantConversationMessage>();
        if (latestCompressionIndex >= 0)
        {
            var compressionEntry = parsedEntries[latestCompressionIndex];
            if (!string.IsNullOrWhiteSpace(compressionEntry.Content))
            {
                modelHistory.Add(new AssistantConversationMessage
                {
                    Role = "system",
                    Content = compressionEntry.Content
                });
            }

            foreach (var entry in parsedEntries.Skip(latestCompressionIndex + 1))
            {
                if (string.Equals(entry.Type, "system", StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryBuildModelHistoryMessage(entry) is { } message)
                {
                    modelHistory.Add(message);
                }
            }
        }
        else
        {
            foreach (var entry in parsedEntries)
            {
                if (TryBuildModelHistoryMessage(entry) is { } message)
                {
                    modelHistory.Add(message);
                }
            }
        }

        var firstEntry = parsedEntries[0];
        var fileInfo = new FileInfo(transcriptPath);
        return new SessionConversationRecord
        {
            SessionId = sessionId,
            TranscriptPath = transcriptPath,
            WorkingDirectory = string.IsNullOrWhiteSpace(firstEntry.WorkingDirectory)
                ? runtimeProfile.ProjectRoot
                : firstEntry.WorkingDirectory,
            GitBranch = firstEntry.GitBranch,
            StartTime = string.IsNullOrWhiteSpace(firstEntry.Timestamp)
                ? fileInfo.CreationTimeUtc.ToString("O")
                : firstEntry.Timestamp,
            LastUpdated = fileInfo.LastWriteTimeUtc.ToString("O"),
            ModelHistory = modelHistory
        };
    }

    public bool RemoveSession(WorkspacePaths paths, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl");
        if (!File.Exists(transcriptPath))
        {
            return false;
        }

        File.Delete(transcriptPath);
        var metadataPath = chatRecordingService.GetMetadataPath(transcriptPath);
        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }

        return true;
    }

    private static SessionPreview? TryReadSession(
        FileInfo file,
        QwenRuntimeProfile runtimeProfile,
        IChatRecordingService chatRecordingService)
    {
        try
        {
            var metadata = chatRecordingService.TryReadMetadata(file.FullName);
            if (metadata is not null)
            {
                return new SessionPreview
                {
                    SessionId = metadata.SessionId,
                    Title = metadata.Title,
                    LastActivity = metadata.LastUpdatedAt,
                    StartedAt = metadata.StartedAt,
                    LastUpdatedAt = metadata.LastUpdatedAt,
                    Category = string.IsNullOrWhiteSpace(metadata.GitBranch)
                        ? runtimeProfile.ApprovalProfile.DefaultMode
                        : metadata.GitBranch,
                    Mode = DesktopMode.Code,
                    Status = NormalizeSessionStatus(metadata.Status),
                    WorkingDirectory = string.IsNullOrWhiteSpace(metadata.WorkingDirectory)
                        ? runtimeProfile.ProjectRoot
                        : metadata.WorkingDirectory,
                    GitBranch = metadata.GitBranch,
                    MessageCount = metadata.MessageCount,
                    TranscriptPath = file.FullName,
                    MetadataPath = metadata.MetadataPath
                };
            }

            using var stream = file.OpenRead();
            using var reader = new StreamReader(stream);

            string? sessionId = null;
            string? workingDirectory = null;
            string? gitBranch = null;
            string? firstUserPrompt = null;
            string sessionStatus = "resume-ready";
            var messageIds = new HashSet<string>(StringComparer.Ordinal);
            var scannedLines = 0;
            string? line;

            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                scannedLines++;
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                sessionId ??= TryGetString(root, "sessionId");
                workingDirectory ??= TryGetString(root, "cwd");
                gitBranch ??= TryGetString(root, "gitBranch");
                if (TryGetString(root, "status") is { Length: > 0 } status)
                {
                    sessionStatus = NormalizeSessionStatus(status);
                }

                if ((TryGetString(root, "type") is "user" or "assistant") &&
                    TryGetString(root, "uuid") is { Length: > 0 } uuid)
                {
                    messageIds.Add(uuid);
                }

                if (firstUserPrompt is null &&
                    TryGetString(root, "type") == "user" &&
                    TryExtractPrompt(root) is { Length: > 0 } prompt)
                {
                    firstUserPrompt = prompt;
                }

                if (scannedLines >= 200 && firstUserPrompt is not null)
                {
                    break;
                }
            }

            var effectiveSessionId = string.IsNullOrWhiteSpace(sessionId)
                ? Path.GetFileNameWithoutExtension(file.Name)
                : sessionId;
            var effectiveWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? runtimeProfile.ProjectRoot
                : workingDirectory;
            var title = string.IsNullOrWhiteSpace(firstUserPrompt)
                ? $"Session {effectiveSessionId[..Math.Min(8, effectiveSessionId.Length)]}"
                : firstUserPrompt;
            return new SessionPreview
            {
                SessionId = effectiveSessionId,
                Title = title,
                LastActivity = file.LastWriteTimeUtc.ToString("O"),
                StartedAt = file.CreationTimeUtc.ToString("O"),
                LastUpdatedAt = file.LastWriteTimeUtc.ToString("O"),
                Category = string.IsNullOrWhiteSpace(gitBranch)
                    ? runtimeProfile.ApprovalProfile.DefaultMode
                    : gitBranch,
                Mode = DesktopMode.Code,
                Status = sessionStatus,
                WorkingDirectory = effectiveWorkingDirectory,
                GitBranch = gitBranch ?? string.Empty,
                MessageCount = messageIds.Count,
                TranscriptPath = file.FullName,
                MetadataPath = chatRecordingService.GetMetadataPath(file.FullName)
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        TryGetProperty(root, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DesktopSessionEntry ParseEntry(JsonElement root)
    {
        var type = TryGetString(root, "type") ?? "unknown";
        var toolName = TryGetString(root, "toolName") ?? TryGetString(root, "commandName") ?? string.Empty;
        var status = TryGetString(root, "status") ?? string.Empty;
        var approvalState = TryGetString(root, "approvalState") ?? string.Empty;
        var body = type switch
        {
            "user" or "assistant" => TryExtractPrompt(root) ?? string.Empty,
            "command" => FirstNonEmpty(
                TryGetString(root, "output"),
                TryGetString(root, "resolvedPrompt"),
                TryGetString(root, "errorMessage")),
            "tool" => FirstNonEmpty(
                TryGetString(root, "output"),
                TryGetString(root, "errorMessage"),
                TryGetString(root, "approvalState")),
            "system" => FirstNonEmpty(
                TryGetString(root, "messageText"),
                TryGetString(root, "status")),
            _ => string.Empty
        };

        return new DesktopSessionEntry
        {
            Id = TryGetString(root, "uuid") ?? Guid.NewGuid().ToString(),
            Type = type,
            Timestamp = TryGetString(root, "timestamp") ?? string.Empty,
            WorkingDirectory = TryGetString(root, "cwd") ?? string.Empty,
            GitBranch = TryGetString(root, "gitBranch") ?? string.Empty,
            Title = type switch
            {
                "user" => "User",
                "assistant" => "Assistant",
                "command" when !string.IsNullOrWhiteSpace(toolName) => $"/{toolName}",
                "tool" when !string.IsNullOrWhiteSpace(toolName) => toolName,
                "system" => "System",
                _ => type
            },
            Body = body,
            Status = status,
            ToolName = toolName,
            ApprovalState = approvalState,
            ExitCode = TryGetInt(root, "exitCode"),
            Arguments = TryGetString(root, "args") ?? string.Empty,
            Scope = TryGetString(root, "scope") ?? string.Empty,
            SourcePath = TryGetString(root, "sourcePath") ?? string.Empty,
            ResolutionStatus = TryGetString(root, "resolutionStatus") ?? string.Empty,
            ResolvedAt = TryGetString(root, "resolvedAt") ?? string.Empty,
            ChangedFiles = TryGetStringArray(root, "changedFiles"),
            Questions = TryGetQuestions(root),
            Answers = TryGetAnswers(root)
        };
    }

    private static DesktopSessionEntry SanitizeEntryForDesktopProjection(DesktopSessionEntry entry) =>
        new()
        {
            Id = entry.Id,
            Type = entry.Type,
            Timestamp = entry.Timestamp,
            WorkingDirectory = entry.WorkingDirectory,
            GitBranch = entry.GitBranch,
            Title = entry.Title,
            Body = Truncate(entry.Body, MaximumBodyLength),
            Status = entry.Status,
            ToolName = entry.ToolName,
            ApprovalState = entry.ApprovalState,
            ExitCode = entry.ExitCode,
            Arguments = Truncate(entry.Arguments, MaximumArgumentsLength),
            Scope = entry.Scope,
            SourcePath = Truncate(entry.SourcePath, MaximumSourcePathLength),
            ResolutionStatus = entry.ResolutionStatus,
            ResolvedAt = entry.ResolvedAt,
            ChangedFiles = entry.ChangedFiles.Take(MaximumChangedFileCount).ToArray(),
            Questions = entry.Questions,
            Answers = entry.Answers
        };

    private static string? TryExtractPrompt(JsonElement root)
    {
        if (!TryGetProperty(root, "message", out var message) ||
            message.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(message, "parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object &&
                TryGetProperty(part, "text", out var textValue) &&
                textValue.ValueKind == JsonValueKind.String)
            {
                var text = textValue.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Length > 140 ? $"{text[..140]}..." : text;
                }
            }
        }

        return null;
    }

    private static int? TryGetInt(JsonElement root, string propertyName) =>
        TryGetProperty(root, propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

    private static string NormalizeSessionStatus(string status) =>
        status switch
        {
            "completed" or "assistant-completed" or "tool-executed" or "answered" or "approved" or "started" => "resume-ready",
            _ => status
        };

    private static IReadOnlyList<string> TryGetStringArray(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<DesktopQuestionPrompt> TryGetQuestions(JsonElement root)
    {
        if (!TryGetProperty(root, "questions", out var questionsElement) || questionsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return questionsElement.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.Object)
            .Select(ParseQuestion)
            .ToArray();
    }

    private static DesktopQuestionPrompt ParseQuestion(JsonElement questionElement)
    {
        var options = TryGetProperty(questionElement, "options", out var optionsElement) &&
            optionsElement.ValueKind == JsonValueKind.Array
            ? optionsElement.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.Object)
                .Select(static item => new DesktopQuestionOption
                {
                    Label = TryGetString(item, "label") ?? string.Empty,
                    Description = TryGetString(item, "description") ?? string.Empty
                })
                .ToArray()
            : [];

        return new DesktopQuestionPrompt
        {
            Header = TryGetString(questionElement, "header") ?? string.Empty,
            Question = TryGetString(questionElement, "question") ?? string.Empty,
            MultiSelect = TryGetProperty(questionElement, "multiSelect", out var multiSelectElement) &&
                multiSelectElement.ValueKind == JsonValueKind.True,
            Options = options
        };
    }

    private static IReadOnlyList<DesktopQuestionAnswer> TryGetAnswers(JsonElement root)
    {
        if (!TryGetProperty(root, "answers", out var answersElement) || answersElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return answersElement.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.Object)
            .Select(static item => new DesktopQuestionAnswer
            {
                QuestionIndex = TryGetInt(item, "questionIndex") ?? 0,
                Value = TryGetString(item, "value") ?? string.Empty
            })
            .ToArray();
    }

    private static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string Truncate(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maximumLength)
        {
            return value ?? string.Empty;
        }

        return $"{value[..maximumLength]}…";
    }

    private static ParsedTranscriptEntry ParseTranscriptEntry(JsonElement root)
    {
        var type = TryGetString(root, "type") ?? string.Empty;
        var status = TryGetString(root, "status") ?? string.Empty;
        var content = type switch
        {
            "user" or "assistant" => TryExtractPrompt(root) ?? string.Empty,
            "command" => FirstNonEmpty(
                TryGetString(root, "resolvedPrompt"),
                TryGetString(root, "output"),
                TryGetString(root, "errorMessage")),
            "tool" => FirstNonEmpty(
                TryGetString(root, "output"),
                TryGetString(root, "errorMessage"),
                TryGetString(root, "approvalState")),
            "system" => FirstNonEmpty(
                TryGetString(root, "messageText"),
                status),
            _ => string.Empty
        };

        return new ParsedTranscriptEntry(
            type,
            status,
            content,
            TryGetString(root, "timestamp") ?? string.Empty,
            TryGetString(root, "cwd") ?? string.Empty,
            TryGetString(root, "gitBranch") ?? string.Empty);
    }

    private static AssistantConversationMessage? TryBuildModelHistoryMessage(ParsedTranscriptEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Content))
        {
            return null;
        }

        var role = entry.Type switch
        {
            "user" => "user",
            "assistant" => "assistant",
            "command" or "tool" => "system",
            _ => null
        };

        if (role is null)
        {
            return null;
        }

        return new AssistantConversationMessage
        {
            Role = role,
            Content = entry.Content
        };
    }

    private sealed record ParsedTranscriptEntry(
        string Type,
        string Status,
        string Content,
        string Timestamp,
        string WorkingDirectory,
        string GitBranch);

}
