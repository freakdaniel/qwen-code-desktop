using System.Text.Json;
using QwenCode.App.Models;
using QwenCode.App.Compatibility;
using QwenCode.App.Runtime;

namespace QwenCode.App.Sessions;

/// <summary>
/// Represents the Desktop Session Catalog Service
/// </summary>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="chatRecordingService">The chat recording service</param>
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

    /// <summary>
    /// Lists sessions
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="limit">The limit</param>
    /// <returns>The resulting i read only list session preview</returns>
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

    /// <summary>
    /// Gets session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting desktop session detail?</returns>
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
                entries.AddRange(ParseEntries(root));
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

    /// <summary>
    /// Executes session exists
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool SessionExists(WorkspacePaths paths, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        return File.Exists(Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl"));
    }

    /// <summary>
    /// Loads last session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting session preview?</returns>
    public SessionPreview? LoadLastSession(WorkspacePaths paths) =>
        ListSessions(paths, 1).FirstOrDefault();

    /// <summary>
    /// Loads conversation
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>The resulting session conversation record?</returns>
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

    /// <summary>
    /// Removes session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
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
            "user" or "assistant" => TryExtractFullText(root) ?? string.Empty,
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

        var thinkingBody = type == "assistant" ? TryExtractThinkingText(root) ?? string.Empty : string.Empty;

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
            ThinkingBody = thinkingBody,
            ThinkingDurationMs = TryGetLong(root, "durationMs") ?? TryGetNestedLong(root, "message", "durationMs") ?? 0,
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

    /// <summary>
    /// Parses a JSONL root element into one or more entries.
    /// For assistant entries with embedded function calls, also emits synthetic tool entries
    /// </summary>
    private static IEnumerable<DesktopSessionEntry> ParseEntries(JsonElement root)
    {
        var main = ParseEntry(root);
        yield return main;

        // Emit synthetic tool entries for each functionCall embedded in an assistant message
        if (main.Type != "assistant") yield break;
        if (!TryGetProperty(root, "message", out var message) || message.ValueKind != JsonValueKind.Object) yield break;
        if (!TryGetProperty(message, "parts", out var parts) || parts.ValueKind != JsonValueKind.Array) yield break;

        var i = 0;
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object) continue;
            if (!TryGetProperty(part, "functionCall", out var fc) || fc.ValueKind != JsonValueKind.Object) continue;

            var callId = TryGetString(fc, "id") ?? $"{main.Id}-fc-{i}";
            var callName = TryGetString(fc, "name") ?? "tool";
            var callArgs = string.Empty;
            if (TryGetProperty(fc, "args", out var argsElement))
            {
                callArgs = argsElement.ValueKind == JsonValueKind.Object || argsElement.ValueKind == JsonValueKind.Array
                    ? argsElement.ToString()
                    : TryGetString(fc, "args") ?? string.Empty;
            }

            yield return new DesktopSessionEntry
            {
                Id = callId,
                Type = "tool",
                Timestamp = main.Timestamp,
                WorkingDirectory = main.WorkingDirectory,
                GitBranch = main.GitBranch,
                Title = callName,
                ToolName = callName,
                Body = string.Empty,
                Status = "completed",
                Arguments = callArgs,
            };
            i++;
        }
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
            ThinkingBody = Truncate(entry.ThinkingBody, MaximumBodyLength),
            ThinkingDurationMs = entry.ThinkingDurationMs,
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

    /// <summary>
    /// Extracts the full message text from a JSONL entry without truncation.
    /// Skips thought/reasoning parts and returns only the actual response text
    /// </summary>
    private static string? TryExtractFullText(JsonElement root)
    {
        if (!TryGetProperty(root, "message", out var message) ||
            message.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Google/Qwen format: message.parts[].text  (skip parts with "thought": true)
        if (TryGetProperty(message, "parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object) continue;

                // Skip thought/reasoning parts
                var isThought = TryGetProperty(part, "thought", out var thoughtFlag) &&
                    thoughtFlag.ValueKind == JsonValueKind.True;
                if (isThought) continue;

                if (TryGetProperty(part, "text", out var textValue) &&
                    textValue.ValueKind == JsonValueKind.String)
                {
                    var text = textValue.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(text);
                    }
                }
            }
            if (sb.Length > 0) return sb.ToString();
        }

        // Anthropic/Claude format: message.content (string)
        if (TryGetProperty(message, "content", out var content))
        {
            if (content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            // Anthropic/Claude format: message.content (array of blocks, skip "thinking" type)
            else if (content.ValueKind == JsonValueKind.Array)
            {
                var sb2 = new System.Text.StringBuilder();
                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object) continue;
                    if (!TryGetProperty(block, "type", out var blockType)) continue;
                    var blockTypeName = blockType.GetString();
                    if (blockTypeName != "text") continue; // skip "thinking" blocks
                    if (!TryGetProperty(block, "text", out var blockText) ||
                        blockText.ValueKind != JsonValueKind.String) continue;

                    var text = blockText.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (sb2.Length > 0) sb2.Append('\n');
                        sb2.Append(text);
                    }
                }
                if (sb2.Length > 0) return sb2.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts only the thinking/reasoning text from an assistant entry
    /// </summary>
    private static string? TryExtractThinkingText(JsonElement root)
    {
        if (!TryGetProperty(root, "message", out var message) ||
            message.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Google/Qwen format: parts with "thought": true
        if (TryGetProperty(message, "parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object) continue;

                var isThought = TryGetProperty(part, "thought", out var thoughtFlag) &&
                    thoughtFlag.ValueKind == JsonValueKind.True;
                if (!isThought) continue;

                if (TryGetProperty(part, "text", out var textValue) &&
                    textValue.ValueKind == JsonValueKind.String)
                {
                    var text = textValue.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append(text);
                    }
                }
            }
            if (sb.Length > 0) return sb.ToString();
        }

        // Anthropic/Claude format: content blocks with "type": "thinking"
        if (TryGetProperty(message, "content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            var sb2 = new System.Text.StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object) continue;
                if (!TryGetProperty(block, "type", out var blockType)) continue;
                if (blockType.GetString() != "thinking") continue;
                if (!TryGetProperty(block, "thinking", out var thinkingText) &&
                    !TryGetProperty(block, "text", out thinkingText)) continue;
                if (thinkingText.ValueKind != JsonValueKind.String) continue;

                var text = thinkingText.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (sb2.Length > 0) sb2.Append('\n');
                    sb2.Append(text);
                }
            }
            if (sb2.Length > 0) return sb2.ToString();
        }

        return null;
    }

    private static int? TryGetInt(JsonElement root, string propertyName) =>
        TryGetProperty(root, propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

    private static long? TryGetLong(JsonElement root, string propertyName) =>
        TryGetProperty(root, propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result)
            ? result
            : null;

    private static long? TryGetNestedLong(JsonElement root, string objectPropertyName, string nestedPropertyName)
    {
        if (!TryGetProperty(root, objectPropertyName, out var nestedRoot) || nestedRoot.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetLong(nestedRoot, nestedPropertyName);
    }

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
            "user" or "assistant" => TryExtractFullText(root) ?? string.Empty,
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
