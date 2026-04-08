using System.Globalization;

namespace QwenCode.App.Runtime;

internal static class RuntimeLocaleCatalog
{
    private static readonly IReadOnlyDictionary<string, string> SupportedLanguages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English",
            ["ru"] = "Russian",
            ["zh-CN"] = "Chinese",
            ["de"] = "German",
            ["fr"] = "French",
            ["es"] = "Spanish",
            ["ja"] = "Japanese",
            ["ko"] = "Korean",
            ["pt-BR"] = "Portuguese (Brazil)",
            ["tr"] = "Turkish",
            ["ar"] = "Arabic"
        };

    public static string DetectLocale(string? configuredLocale = null)
    {
        var envLang = Environment.GetEnvironmentVariable("QWEN_CODE_LANG");
        if (!string.IsNullOrWhiteSpace(envLang))
        {
            return NormalizeLocale(envLang.Trim());
        }

        var lang = Environment.GetEnvironmentVariable("LANG");
        if (!string.IsNullOrWhiteSpace(lang))
        {
            var langCode = lang.Split('.', '_')[0];
            return NormalizeLocale(langCode);
        }

        try
        {
            var culture = CultureInfo.CurrentUICulture;
            if (culture is not null)
            {
                var normalized = NormalizeLocale(culture.Name);
                if (!string.Equals(normalized, "en", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(culture.Name, "en", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(culture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase))
                {
                    return normalized;
                }

                return NormalizeLocale(culture.TwoLetterISOLanguageName);
            }
        }
        catch
        {
            // Ignore locale detection issues and fall back below.
        }

        return string.IsNullOrWhiteSpace(configuredLocale)
            ? "en"
            : NormalizeLocale(configuredLocale);
    }

    public static string NormalizeLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return "en";
        }

        var exact = SupportedLanguages.Keys.FirstOrDefault(key =>
            string.Equals(key, locale, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        var language = locale.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
        var fallback = SupportedLanguages.Keys.FirstOrDefault(key =>
            string.Equals(
                key.Split('-', StringSplitOptions.RemoveEmptyEntries)[0],
                language,
                StringComparison.OrdinalIgnoreCase));

        return fallback ?? "en";
    }

    public static string ResolveLanguageName(string locale)
    {
        var normalized = NormalizeLocale(locale);
        return SupportedLanguages.TryGetValue(normalized, out var language)
            ? language
            : "English";
    }
}
