using System.Text.Json;
using Microsoft.Extensions.Logging;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;
using QwenCode.App.Runtime;
using QwenCode.App.Sessions;

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
        ["ru-RU"] = "Russian",
        ["zh-CN"] = "Chinese",
        ["zh-TW"] = "Chinese",
        ["ja-JP"] = "Japanese",
        ["ko-KR"] = "Korean",
        ["pt-BR"] = "Portuguese",
        ["pt-PT"] = "Portuguese",
        ["de-DE"] = "German",
        ["fr-FR"] = "French",
        ["es-ES"] = "Spanish",
        ["it-IT"] = "Italian",
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
        if (!string.IsNullOrEmpty(locale) && LocaleLanguageMap.TryGetValue(locale, out var name))
        {
            return name;
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
            var language = ResolveLanguageName(locale);
            var systemPrompt =
                $"Generate a concise 5-7 word title for a coding session.\nReply in {language}.\nReply with ONLY the title. No quotes, no punctuation at the end.";

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

            return response.Content.Trim();
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
