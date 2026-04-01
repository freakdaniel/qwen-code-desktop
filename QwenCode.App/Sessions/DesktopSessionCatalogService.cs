using System.Text.Json;
using QwenCode.App.Enums;
using QwenCode.App.Models;
using QwenCode.App.Compatibility;

namespace QwenCode.App.Sessions;

public sealed class DesktopSessionCatalogService(QwenRuntimeProfileService runtimeProfileService) : ITranscriptStore
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    public IReadOnlyList<SessionPreview> ListSessions(SourceMirrorPaths paths, int limit = 24)
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
            .Select(file => TryReadSession(file, runtimeProfile))
            .OfType<SessionPreview>()
            .ToArray();

        return sessions;
    }

    public DesktopSessionDetail? GetSession(SourceMirrorPaths paths, string sessionId)
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

        var preview = ListSessions(paths, 128).FirstOrDefault(item =>
            string.Equals(item.SessionId, sessionId, StringComparison.Ordinal));
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

        return new DesktopSessionDetail
        {
            Session = preview,
            TranscriptPath = transcriptPath,
            EntryCount = entries.Count,
            Summary = DesktopSessionActivitySummaryBuilder.Build(entries),
            Entries = entries
        };
    }

    private static SessionPreview? TryReadSession(FileInfo file, QwenRuntimeProfile runtimeProfile)
    {
        try
        {
            using var stream = file.OpenRead();
            using var reader = new StreamReader(stream);

            string? sessionId = null;
            string? workingDirectory = null;
            string? gitBranch = null;
            string? firstUserPrompt = null;
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
                LastActivity = FormatRelativeTime(file.LastWriteTimeUtc, DateTime.UtcNow),
                Category = string.IsNullOrWhiteSpace(gitBranch)
                    ? runtimeProfile.ApprovalProfile.DefaultMode
                    : gitBranch,
                Mode = DesktopMode.Code,
                Status = "resume-ready",
                WorkingDirectory = effectiveWorkingDirectory,
                GitBranch = gitBranch ?? string.Empty,
                MessageCount = messageIds.Count,
                TranscriptPath = file.FullName
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
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
            ChangedFiles = TryGetStringArray(root, "changedFiles")
        };
    }

    private static string? TryExtractPrompt(JsonElement root)
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
                part.TryGetProperty("text", out var textValue) &&
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
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

    private static IReadOnlyList<string> TryGetStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
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

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string FormatRelativeTime(DateTime timestampUtc, DateTime nowUtc)
    {
        var delta = nowUtc - timestampUtc;
        if (delta < Minute)
        {
            return "Updated just now";
        }

        if (delta < Hour)
        {
            return $"Updated {(int)delta.TotalMinutes} minute{Pluralize(delta.TotalMinutes)} ago";
        }

        if (delta < Day)
        {
            return $"Updated {(int)delta.TotalHours} hour{Pluralize(delta.TotalHours)} ago";
        }

        return $"Updated {(int)delta.TotalDays} day{Pluralize(delta.TotalDays)} ago";
    }

    private static string Pluralize(double value) => Math.Abs(value) >= 2 ? "s" : string.Empty;
}
