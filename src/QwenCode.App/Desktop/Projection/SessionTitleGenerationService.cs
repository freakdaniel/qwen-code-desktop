using System.Text.Json;
using Microsoft.Extensions.Logging;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Models;
using QwenCode.Core.Runtime;
using QwenCode.Core.Sessions;

namespace QwenCode.App.Desktop.Projection;

/// <summary>
/// Represents the Session Title Generation Service
/// </summary>
/// <param name="contentGenerator">The content generator</param>
/// <param name="chatRecordingService">The chat recording service</param>
/// <param name="sessionEventPublisher">The session event publisher</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="logger">The logger</param>
public sealed class SessionTitleGenerationService(
    IContentGenerator contentGenerator,
    IChatRecordingService chatRecordingService,
    ISessionEventPublisher sessionEventPublisher,
    QwenRuntimeProfileService runtimeProfileService,
    ILogger<SessionTitleGenerationService> logger) : ISessionTitleGenerationService
{
    private static readonly Dictionary<string, string> LocaleLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ru"] = "Russian",
        ["ru-RU"] = "Russian",
        ["zh"] = "Chinese",
        ["zh-CN"] = "Chinese",
        ["zh-TW"] = "Chinese",
        ["ja"] = "Japanese",
        ["ja-JP"] = "Japanese",
        ["ko"] = "Korean",
        ["ko-KR"] = "Korean",
        ["pt"] = "Portuguese",
        ["pt-BR"] = "Portuguese",
        ["pt-PT"] = "Portuguese",
        ["de"] = "German",
        ["de-DE"] = "German",
        ["fr"] = "French",
        ["fr-FR"] = "French",
        ["es"] = "Spanish",
        ["es-ES"] = "Spanish",
        ["it"] = "Italian",
        ["it-IT"] = "Italian",
        ["en"] = "English",
        ["en-US"] = "English",
        ["en-GB"] = "English"
    };

    private readonly SemaphoreSlim _semaphore = new(6, 6);

    /// <summary>
    /// Resolves the human-readable language name for the given locale code.
    /// Falls back to "English" for unknown locales
    /// </summary>
    /// <param name="locale">The locale code (e.g. "ru-RU")</param>
    /// <returns>The language name</returns>
    public static string ResolveLanguageName(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return "English";
        }

        if (LocaleLanguageMap.TryGetValue(locale, out var exactName))
        {
            return exactName;
        }

        var languageCode = locale.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
        if (!string.IsNullOrWhiteSpace(languageCode) && LocaleLanguageMap.TryGetValue(languageCode, out var shortName))
        {
            return shortName;
        }

        return "English";
    }

    /// <summary>
    /// Builds a fallback title by truncating the text at 60 characters and appending "...".
    /// Returns the text as-is if it is 60 characters or fewer
    /// </summary>
    /// <param name="text">The source text</param>
    /// <returns>The fallback title</returns>
    public static string BuildFallbackTitle(string text)
    {
        if (text.Length <= 60)
        {
            return text;
        }

        return text[..60] + "...";
    }

    /// <summary>
    /// Builds the utility system prompt used to generate chat titles.
    /// </summary>
    /// <param name="locale">The UI locale code</param>
    /// <returns>The title generation system prompt</returns>
    public static string BuildTitleSystemPrompt(string locale)
    {
        var normalizedLocale = string.IsNullOrWhiteSpace(locale) ? "en-US" : locale;
        var language = ResolveLanguageName(normalizedLocale);
        return NativeAssistantUtilityPromptCatalog.BuildSessionTitlePrompt(normalizedLocale, language);
    }

    /// <summary>
    /// Normalizes a model-generated title into a compact single-line label.
    /// </summary>
    /// <param name="rawTitle">The raw title output</param>
    /// <returns>The normalized title or an empty string</returns>
    public static string NormalizeGeneratedTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            return string.Empty;
        }

        var normalized = rawTitle
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        normalized = normalized.Trim('"', '\'', '\u00AB', '\u00BB', '\u201C', '\u201D', '`', ' ', '\t');
        normalized = normalized.TrimEnd('.', '!', '?', ';', ':', ',', '\u2026');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var words = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(7)
            .ToArray();

        return string.Join(' ', words).Trim();
    }

    /// <inheritdoc/>
    public void EnqueueTitleGeneration(
        string sessionId,
        string firstMessageText,
        string transcriptPath,
        string workingDirectory,
        string locale)
    {
        _ = Task.Run(() => GenerateAsync(sessionId, firstMessageText, transcriptPath, workingDirectory, locale));
    }

    private async Task GenerateAsync(
        string sessionId,
        string firstMessageText,
        string transcriptPath,
        string workingDirectory,
        string locale)
    {
        await _semaphore.WaitAsync();
        try
        {
            var title = await TryGenerateTitleFromLlmAsync(
                sessionId, firstMessageText, transcriptPath, workingDirectory, locale);

            if (string.IsNullOrWhiteSpace(title))
            {
                title = BuildFallbackTitle(firstMessageText);
            }

            await WriteTitleToMetadataAsync(transcriptPath, title);

            sessionEventPublisher.Publish(new DesktopSessionEvent
            {
                SessionId = sessionId,
                Kind = DesktopSessionEventKind.SessionTitleUpdated,
                TimestampUtc = DateTime.UtcNow,
                Message = string.Empty,
                Title = title
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Background title generation failed for session {SessionId}.", sessionId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string?> TryGenerateTitleFromLlmAsync(
        string sessionId,
        string firstMessageText,
        string transcriptPath,
        string workingDirectory,
        string locale)
    {
        try
        {
            var systemPrompt = BuildTitleSystemPrompt(locale);

            var truncatedPrompt = firstMessageText.Length > 500
                ? firstMessageText[..500]
                : firstMessageText;

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths
            {
                WorkspaceRoot = workingDirectory
            });

            var promptContext = new AssistantPromptContext
            {
                Messages = [],
                ContextFiles = [],
                HistoryHighlights = []
            };

            var response = await contentGenerator.GenerateContentAsync(
                new LlmContentRequest
                {
                    SessionId = sessionId,
                    Prompt = truncatedPrompt,
                    WorkingDirectory = workingDirectory,
                    TranscriptPath = transcriptPath,
                    RuntimeProfile = runtimeProfile,
                    PromptContext = promptContext,
                    SystemPrompt = systemPrompt,
                    DisableTools = true
                });

            if (response is null || string.IsNullOrWhiteSpace(response.Content))
            {
                return null;
            }

            var normalizedTitle = NormalizeGeneratedTitle(response.Content);
            return string.IsNullOrWhiteSpace(normalizedTitle)
                ? null
                : normalizedTitle;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM title generation call failed for session {SessionId}; using fallback.", sessionId);
            return null;
        }
    }

    private async Task WriteTitleToMetadataAsync(string transcriptPath, string title)
    {
        var metadataPath = chatRecordingService.GetMetadataPath(transcriptPath);
        var existing = chatRecordingService.TryReadMetadata(transcriptPath);

        SessionRecordingMetadata updated;
        if (existing is not null)
        {
            updated = new SessionRecordingMetadata
            {
                SessionId = existing.SessionId,
                TranscriptPath = existing.TranscriptPath,
                MetadataPath = existing.MetadataPath,
                Title = title,
                WorkingDirectory = existing.WorkingDirectory,
                GitBranch = existing.GitBranch,
                Status = existing.Status,
                StartedAt = existing.StartedAt,
                LastUpdatedAt = DateTime.UtcNow.ToString("O"),
                LastCompletedUuid = existing.LastCompletedUuid,
                MessageCount = existing.MessageCount,
                EntryCount = existing.EntryCount
            };
        }
        else
        {
            var sessionId = Path.GetFileNameWithoutExtension(transcriptPath);
            updated = new SessionRecordingMetadata
            {
                SessionId = sessionId,
                TranscriptPath = transcriptPath,
                MetadataPath = metadataPath,
                Title = title,
                WorkingDirectory = string.Empty,
                GitBranch = string.Empty,
                Status = "active",
                StartedAt = DateTime.UtcNow.ToString("O"),
                LastUpdatedAt = DateTime.UtcNow.ToString("O"),
                LastCompletedUuid = string.Empty
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(metadataPath) ?? string.Empty);
        await File.WriteAllTextAsync(
            metadataPath,
            JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
    }
}
