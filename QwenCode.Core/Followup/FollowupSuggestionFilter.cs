namespace QwenCode.App.Followup;

internal static class FollowupSuggestionFilter
{
    private static readonly HashSet<string> AllowedSingleWords =
    [
        "yes",
        "yeah",
        "yep",
        "yea",
        "yup",
        "sure",
        "ok",
        "okay",
        "push",
        "commit",
        "deploy",
        "stop",
        "continue",
        "check",
        "exit",
        "quit",
        "no"
    ];

    public static string? GetFilterReason(string suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion))
        {
            return "empty";
        }

        var trimmed = suggestion.Trim();
        var lower = trimmed.ToLowerInvariant();
        var wordCount = trimmed.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

        if (lower == "done")
        {
            return "done";
        }

        if (lower == "nothing found" ||
            lower == "nothing found." ||
            lower.StartsWith("nothing to suggest", StringComparison.Ordinal) ||
            lower.StartsWith("no suggestion", StringComparison.Ordinal) ||
            lower.Contains("stay silent", StringComparison.Ordinal) ||
            lower == "silence")
        {
            return "meta_text";
        }

        if ((trimmed.StartsWith("(") && trimmed.EndsWith(")")) ||
            (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
        {
            return "meta_wrapped";
        }

        if (lower.StartsWith("api error:", StringComparison.Ordinal) ||
            lower.StartsWith("prompt is too long", StringComparison.Ordinal) ||
            lower.StartsWith("request timed out", StringComparison.Ordinal) ||
            lower.StartsWith("invalid api key", StringComparison.Ordinal) ||
            lower.StartsWith("image was too large", StringComparison.Ordinal))
        {
            return "error_message";
        }

        if (trimmed.Length >= 100)
        {
            return "too_long";
        }

        var hasCjk = trimmed.Any(IsCjkCharacter);
        if (!hasCjk)
        {
            if (wordCount < 2)
            {
                if (!trimmed.StartsWith("/", StringComparison.Ordinal) &&
                    !AllowedSingleWords.Contains(lower))
                {
                    return "too_few_words";
                }
            }

            if (wordCount > 12)
            {
                return "too_many_words";
            }
        }
        else
        {
            if (trimmed.Length < 2)
            {
                return "too_few_words";
            }

            if (trimmed.Length > 30)
            {
                return "too_many_words";
            }
        }

        if (trimmed.Contains('\n') || trimmed.Contains('*'))
        {
            return "has_formatting";
        }

        if (lower.Contains("thanks", StringComparison.Ordinal) ||
            lower.Contains("thank you", StringComparison.Ordinal) ||
            lower.Contains("looks good", StringComparison.Ordinal) ||
            lower.Contains("sounds good", StringComparison.Ordinal) ||
            lower.Contains("that works", StringComparison.Ordinal) ||
            lower.Contains("great", StringComparison.Ordinal) ||
            lower.Contains("perfect", StringComparison.Ordinal) ||
            lower.Contains("awesome", StringComparison.Ordinal))
        {
            return "evaluative";
        }

        if (StartsWithAiVoice(trimmed))
        {
            return "ai_voice";
        }

        if (trimmed.Contains('?'))
        {
            return "question";
        }

        return null;
    }

    public static string Normalize(string suggestion)
    {
        var trimmed = suggestion.Trim();
        return trimmed
            .Trim('"', '\'', '`')
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static bool StartsWithAiVoice(string suggestion) =>
        suggestion.StartsWith("Let me", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("I'll", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("I've", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("I'm", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("I can", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("I would", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("I think", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("I notice", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("Here's", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("Here is", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("You can", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("You should", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("Sure,", StringComparison.OrdinalIgnoreCase) ||
        suggestion.StartsWith("Of course", StringComparison.OrdinalIgnoreCase);

    private static bool IsCjkCharacter(char character) =>
        character is >= '\u4e00' and <= '\u9fff' ||
        character is >= '\u3040' and <= '\u309f' ||
        character is >= '\u30a0' and <= '\u30ff' ||
        character is >= '\uac00' and <= '\ud7af';
}
