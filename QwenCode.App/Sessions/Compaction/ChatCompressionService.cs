using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public sealed class ChatCompressionService : IChatCompressionService
{
    private const int CompressionThresholdEntries = 20;
    private const int PreserveEntries = 8;
    private const int RecentCompressionWindow = 6;
    private const int MaxBulletCount = 12;
    private const int MaxBulletLength = 180;

    public Task<ChatCompressionCheckpoint?> TryCreateCheckpointAsync(
        string transcriptPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptPath) || !File.Exists(transcriptPath))
        {
            return Task.FromResult<ChatCompressionCheckpoint?>(null);
        }

        var entries = new List<CompressionEntry>();
        foreach (var line in File.ReadLines(transcriptPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                entries.Add(new CompressionEntry(
                    TryGetString(root, "type") ?? string.Empty,
                    TryGetString(root, "status") ?? string.Empty,
                    TryGetString(root, "toolName") ?? TryGetString(root, "commandName") ?? string.Empty,
                    ExtractBody(root)));
            }
            catch
            {
                // Keep compaction best-effort on partially malformed transcripts.
            }
        }

        if (entries.Count <= CompressionThresholdEntries)
        {
            return Task.FromResult<ChatCompressionCheckpoint?>(null);
        }

        if (entries.TakeLast(RecentCompressionWindow)
            .Any(static entry => entry.Type == "system" &&
                                 string.Equals(entry.Status, "chat-compression", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult<ChatCompressionCheckpoint?>(null);
        }

        var compressibleEntries = entries.Take(Math.Max(0, entries.Count - PreserveEntries)).ToArray();
        if (compressibleEntries.Length == 0)
        {
            return Task.FromResult<ChatCompressionCheckpoint?>(null);
        }

        var bullets = compressibleEntries
            .TakeLast(MaxBulletCount)
            .Select(BuildBullet)
            .Where(static bullet => !string.IsNullOrWhiteSpace(bullet))
            .ToArray();
        if (bullets.Length == 0)
        {
            return Task.FromResult<ChatCompressionCheckpoint?>(null);
        }

        var summary =
            $"Compression checkpoint: summarized {compressibleEntries.Length} earlier transcript entries and preserved the latest {Math.Min(PreserveEntries, entries.Count)} entries." +
            Environment.NewLine +
            string.Join(Environment.NewLine, bullets);

        return Task.FromResult<ChatCompressionCheckpoint?>(new ChatCompressionCheckpoint
        {
            Summary = summary,
            CompressedEntryCount = compressibleEntries.Length,
            PreservedEntryCount = Math.Min(PreserveEntries, entries.Count)
        });
    }

    private static string BuildBullet(CompressionEntry entry)
    {
        var label = entry.Type switch
        {
            "user" => "User",
            "assistant" => "Assistant",
            "command" when !string.IsNullOrWhiteSpace(entry.Name) => $"/{entry.Name}",
            "tool" when !string.IsNullOrWhiteSpace(entry.Name) => $"Tool {entry.Name}",
            "system" => "System",
            _ => "Entry"
        };

        var body = string.IsNullOrWhiteSpace(entry.Body)
            ? entry.Status
            : entry.Body;
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var trimmed = body.Length <= MaxBulletLength ? body : $"{body[..MaxBulletLength]}...";
        return $"- {label}: {trimmed}";
    }

    private static string ExtractBody(JsonElement root)
    {
        var type = TryGetString(root, "type") ?? string.Empty;
        return type switch
        {
            "user" or "assistant" => ExtractMessageText(root),
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
    }

    private static string ExtractMessageText(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) ||
            message.ValueKind != JsonValueKind.Object ||
            !message.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object &&
                part.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
            {
                var value = text.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return string.Empty;
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record CompressionEntry(string Type, string Status, string Name, string Body);
}
