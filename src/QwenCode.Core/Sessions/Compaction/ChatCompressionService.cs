using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

/// <summary>
/// Represents the Chat Compression Service
/// </summary>
public sealed class ChatCompressionService : IChatCompressionService
{
    private const int MinimumCompressionEntries = 12;
    private const int PreserveEntries = 8;
    private const int RecentCompressionWindow = 6;
    private const int MaxBulletCount = 12;
    private const int MaxBulletLength = 180;
    private const double DefaultContextThreshold = 0.72d;

    /// <summary>
    /// Attempts to create checkpoint async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="transcriptPath">The transcript path</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to chat compression checkpoint?</returns>
    public Task<ChatCompressionCheckpoint?> TryCreateCheckpointAsync(
        QwenRuntimeProfile runtimeProfile,
        string transcriptPath,
        CancellationToken cancellationToken = default)
    {
        if (!runtimeProfile.Checkpointing)
        {
            return Task.FromResult<ChatCompressionCheckpoint?>(null);
        }

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

        if (entries.Count < MinimumCompressionEntries)
        {
            return Task.FromResult<ChatCompressionCheckpoint?>(null);
        }

        if (entries.TakeLast(RecentCompressionWindow)
            .Any(static entry => entry.Type == "system" &&
                                 string.Equals(entry.Status, "chat-compression", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult<ChatCompressionCheckpoint?>(null);
        }

        var estimatedTokenCount = entries.Sum(static entry => EstimateTokens(entry.Body, entry.Name, entry.Status));
        var contextWindowTokens = InferContextWindowTokens(runtimeProfile.ModelName);
        var estimatedContextPercentage = contextWindowTokens <= 0
            ? 0d
            : (double)estimatedTokenCount / contextWindowTokens;
        var thresholdPercentage = runtimeProfile.ChatCompression?.ContextPercentageThreshold ?? DefaultContextThreshold;
        if (estimatedContextPercentage < thresholdPercentage)
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
            $"Compression checkpoint: estimated {estimatedTokenCount} of {contextWindowTokens} tokens ({estimatedContextPercentage:P1}) " +
            $"and summarized {compressibleEntries.Length} earlier transcript entries while preserving the latest {Math.Min(PreserveEntries, entries.Count)} entries." +
            Environment.NewLine +
            string.Join(Environment.NewLine, bullets);

        return Task.FromResult<ChatCompressionCheckpoint?>(new ChatCompressionCheckpoint
        {
            Summary = summary,
            CompressedEntryCount = compressibleEntries.Length,
            PreservedEntryCount = Math.Min(PreserveEntries, entries.Count),
            EstimatedTokenCount = estimatedTokenCount,
            EstimatedContextWindowTokens = contextWindowTokens,
            EstimatedContextPercentage = estimatedContextPercentage,
            ThresholdPercentage = thresholdPercentage,
            Trigger = "context-threshold",
            CreatedAtUtc = DateTime.UtcNow
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

    private static int EstimateTokens(string body, string name, string status)
    {
        var combined = string.Join(" ", new[] { body, name, status }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(combined))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(combined.Length / 4d));
    }

    private static int InferContextWindowTokens(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return 32_000;
        }

        if (modelName.Contains("plus", StringComparison.OrdinalIgnoreCase) ||
            modelName.Contains("max", StringComparison.OrdinalIgnoreCase))
        {
            return 131_072;
        }

        if (modelName.Contains("coder", StringComparison.OrdinalIgnoreCase) ||
            modelName.Contains("qwen3", StringComparison.OrdinalIgnoreCase))
        {
            return 65_536;
        }

        return 32_000;
    }

    private sealed record CompressionEntry(string Type, string Status, string Name, string Body);
}
