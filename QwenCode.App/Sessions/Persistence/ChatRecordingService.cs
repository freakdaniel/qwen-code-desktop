using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public sealed class ChatRecordingService : IChatRecordingService
{
    private const int MaximumTitleLength = 140;

    public string GetMetadataPath(string transcriptPath)
    {
        var directory = Path.GetDirectoryName(transcriptPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(transcriptPath);
        return Path.Combine(directory, $"{fileName}.meta.json");
    }

    public SessionRecordingMetadata? TryReadMetadata(string transcriptPath)
    {
        var metadataPath = GetMetadataPath(transcriptPath);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<SessionRecordingMetadata>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch
        {
            return null;
        }
    }

    public async Task<SessionRecordingMetadata?> RefreshMetadataAsync(
        string transcriptPath,
        SessionRecordingContext context,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(transcriptPath))
        {
            return null;
        }

        string? sessionId = null;
        string? workingDirectory = null;
        string? gitBranch = null;
        string? title = null;
        string? startedAt = null;
        string? lastUpdatedAt = null;
        string? lastCompletedUuid = null;
        var status = NormalizeSessionStatus(context.Status);
        var messageCount = 0;
        var entryCount = 0;

        foreach (var line in await File.ReadAllLinesAsync(transcriptPath, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                entryCount++;

                sessionId ??= TryGetString(root, "sessionId");
                workingDirectory ??= TryGetString(root, "cwd");
                gitBranch ??= TryGetString(root, "gitBranch");
                startedAt ??= TryGetString(root, "timestamp");
                lastUpdatedAt = TryGetString(root, "timestamp") ?? lastUpdatedAt;

                if (TryGetString(root, "uuid") is { Length: > 0 } uuid)
                {
                    lastCompletedUuid = uuid;
                }

                var type = TryGetString(root, "type");
                if (type is "user" or "assistant")
                {
                    messageCount++;
                }

                if (title is null &&
                    type == "user" &&
                    TryExtractPrompt(root) is { Length: > 0 } prompt)
                {
                    title = prompt.Length > MaximumTitleLength ? $"{prompt[..MaximumTitleLength]}..." : prompt;
                }

                if (TryGetString(root, "status") is { Length: > 0 } entryStatus)
                {
                    status = NormalizeSessionStatus(entryStatus);
                }
            }
            catch
            {
                // Keep metadata refresh best-effort for partially malformed transcripts.
            }
        }

        var effectiveSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? Path.GetFileNameWithoutExtension(transcriptPath)
            : sessionId;
        var effectiveStartedAt = startedAt ?? DateTime.UtcNow.ToString("O");
        var effectiveLastUpdatedAt = lastUpdatedAt ?? effectiveStartedAt;
        var effectiveTitle = string.IsNullOrWhiteSpace(title)
            ? BuildFallbackTitle(context.TitleHint, effectiveSessionId)
            : title;

        var metadata = new SessionRecordingMetadata
        {
            SessionId = effectiveSessionId,
            TranscriptPath = transcriptPath,
            MetadataPath = GetMetadataPath(transcriptPath),
            Title = effectiveTitle,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? context.WorkingDirectory
                : workingDirectory,
            GitBranch = string.IsNullOrWhiteSpace(gitBranch)
                ? context.GitBranch
                : gitBranch,
            Status = status,
            StartedAt = effectiveStartedAt,
            LastUpdatedAt = effectiveLastUpdatedAt,
            LastCompletedUuid = lastCompletedUuid ?? string.Empty,
            MessageCount = messageCount,
            EntryCount = entryCount
        };

        Directory.CreateDirectory(Path.GetDirectoryName(metadata.MetadataPath) ?? string.Empty);
        await File.WriteAllTextAsync(
            metadata.MetadataPath,
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = true
            }),
            cancellationToken);

        return metadata;
    }

    private static string BuildFallbackTitle(string titleHint, string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(titleHint))
        {
            return titleHint.Length > MaximumTitleLength ? $"{titleHint[..MaximumTitleLength]}..." : titleHint;
        }

        return $"Session {sessionId[..Math.Min(8, sessionId.Length)]}";
    }

    private static string NormalizeSessionStatus(string status) =>
        status switch
        {
            "completed" or "assistant-completed" or "tool-executed" or "answered" or "approved" or "started" => "resume-ready",
            _ => status
        };

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

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
                    return text;
                }
            }
        }

        return null;
    }
}
