using System.Globalization;
using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

internal static class DesktopProjectionCatalog
{
    internal static readonly IReadOnlyList<LocaleOption> SupportedLocales =
    [
        new() { Code = "en", Name = "English", NativeName = "English" },
        new() { Code = "ru", Name = "Russian", NativeName = "\u0420\u0443\u0441\u0441\u043a\u0438\u0439" },
        new() { Code = "zh-CN", Name = "Chinese", NativeName = "\u7b80\u4f53\u4e2d\u6587" },
        new() { Code = "de", Name = "German", NativeName = "Deutsch" },
        new() { Code = "fr", Name = "French", NativeName = "Fran\u00e7ais" },
        new() { Code = "es", Name = "Spanish", NativeName = "Espa\u00f1ol" },
        new() { Code = "ja", Name = "Japanese", NativeName = "\u65e5\u672c\u8a9e" },
        new() { Code = "ko", Name = "Korean", NativeName = "\ud55c\uad6d\uc5b4" },
        new() { Code = "pt-BR", Name = "Portuguese (Brazil)", NativeName = "Portugu\u00eas (Brasil)" },
        new() { Code = "tr", Name = "Turkish", NativeName = "T\u00fcrk\u00e7e" },
        new() { Code = "ar", Name = "Arabic", NativeName = "\u0627\u0644\u0639\u0631\u0628\u064a\u0629" }
    ];

    /// <summary>
    /// Detects the default locale using priority: QWEN_CODE_LANG env, LANG env,
    /// system CultureInfo, then fallback to "en".
    /// </summary>
    internal static string DetectDefaultLocale(string? configDefaultLocale = null)
    {
        // 1. QWEN_CODE_LANG environment variable (highest priority, matches CLI)
        var envLang = Environment.GetEnvironmentVariable("QWEN_CODE_LANG");
        if (!string.IsNullOrWhiteSpace(envLang))
        {
            var normalized = NormalizeLocale(envLang.Trim());
            if (normalized != "en") return normalized;
        }

        // 2. LANG environment variable (common on Linux/macOS)
        var lang = Environment.GetEnvironmentVariable("LANG");
        if (!string.IsNullOrWhiteSpace(lang))
        {
            // LANG can be like "en_US.UTF-8" — extract language part
            var langCode = lang.Split('.', '_')[0];
            var normalized = NormalizeLocale(langCode);
            if (normalized != "en") return normalized;
        }

        // 3. System CultureInfo
        try
        {
            var culture = CultureInfo.CurrentUICulture;
            if (culture != null)
            {
                // Try full name first (e.g. "zh-CN")
                var normalized = NormalizeLocale(culture.Name);
                if (normalized != "en") return normalized;

                // Try two-letter ISO code
                normalized = NormalizeLocale(culture.TwoLetterISOLanguageName);
                if (normalized != "en") return normalized;
            }
        }
        catch
        {
            // Ignore culture access errors
        }

        // 4. Config default
        if (!string.IsNullOrWhiteSpace(configDefaultLocale))
        {
            return NormalizeLocale(configDefaultLocale);
        }

        // 5. Fallback
        return "en";
    }

    internal static readonly IReadOnlyList<ResearchTrack> Tracks =
    [
        new()
        {
            Title = "Lift qwen core behind a native session host",
            Summary = "The desktop backend should own orchestration, but the model loop, tools, history, and policy logic must stay source-compatible with qwen."
        },
        new()
        {
            Title = "Drive sessions through native desktop ergonomics",
            Summary = "Desktop workspaces should expose sessions, approvals, and activity as first-class GUI concepts instead of terminal-only state."
        },
        new()
        {
            Title = "Keep the Electron bridge narrow and typed",
            Summary = "Typed preload contracts and strongly shaped payloads make the desktop shell maintainable while the runtime grows underneath."
        }
    ];

    internal static readonly IReadOnlyList<string> CompatibilityGoals =
    [
        "Do not shell out to qwen CLI for core execution paths.",
        "Keep .qwen-compatible settings, memory, session, and tool semantics.",
        "Keep every production runtime contract inside this codebase.",
        "Make desktop-specific concerns explicit: windows, trays, approvals, attachments, and session chrome."
    ];

    internal static readonly IReadOnlyList<CapabilityLane> CapabilityLanes =
    [
        new()
        {
            Title = "Qwen runtime lane",
            Summary = "Owns prompt construction, tool registry, session state, settings, and compatibility with qwen workflows.",
            Responsibilities =
            [
                "Model API integration and turn execution",
                "Tool calling, sandbox policy, and approvals",
                "History, memory, slash commands, and settings compatibility"
            ]
        },
        new()
        {
            Title = "Desktop bridge lane",
            Summary = "Owns native session hosting, IPC, attachment plumbing, notifications, and cross-window coordination.",
            Responsibilities =
            [
                "Session bootstrap and reconnect semantics",
                "Typed IPC between .NET host and renderer",
                "Native file pickers, desktop prompts, and platform integrations"
            ]
        },
        new()
        {
            Title = "Renderer workspace lane",
            Summary = "Owns the high-level interaction model: sidebar navigation, code-first desktop surfaces, approvals, and task visibility.",
            Responsibilities =
            [
                "Home, sessions, customize, projects, and artifacts surfaces",
                "Single code-mode composer and conversation chrome",
                "Tool timeline, approval panels, and architecture guidance"
            ]
        }
    ];

    internal static readonly IReadOnlyList<AdoptionPattern> AdoptionPatterns =
    [
        new()
        {
            Area = "Execution engine",
            QwenSource = "Native runtime foundations already own slash commands, session writes, and qwen-compatible policy handling.",
            ClaudeReference = "The remaining gap is a provider-backed model loop with token streaming and tool orchestration.",
            DesktopDirection = "Build a native host around qwen core primitives, not a wrapper around qwen CLI stdout.",
            DeliveryState = "Foundation"
        },
        new()
        {
            Area = "Session lifecycle",
            QwenSource = "Transcript persistence, approvals, and resume flows already live inside the native session engine.",
            ClaudeReference = "The current focus is turning those flows into a richer live desktop workspace with reconnect and activity surfaces.",
            DesktopDirection = "Promote sessions to first-class desktop objects with reconnect, activity, and branch/worktree awareness.",
            DeliveryState = "High priority"
        },
        new()
        {
            Area = "Approvals and tools",
            QwenSource = "Approval modes and sandbox policies already exist in qwen and should be preserved.",
            ClaudeReference = "Permission requests, task state, and tool activity should stay visible in dedicated desktop surfaces.",
            DesktopDirection = "Move approvals into explicit desktop panels while keeping qwen policy logic intact.",
            DeliveryState = "High priority"
        },
        new()
        {
            Area = "Context surfaces",
            QwenSource = "Settings, memory, slash commands, and project context are already well-defined.",
            ClaudeReference = "The desktop shell still needs more discoverable surfaces for connectors, scheduled work, and project context.",
            DesktopDirection = "Expose qwen capabilities as browseable desktop surfaces rather than hidden CLI-only concepts.",
            DeliveryState = "In design"
        },
        new()
        {
            Area = "IPC discipline",
            QwenSource = "Core/CLI separation means renderer-specific contracts should stay outside the engine.",
            ClaudeReference = "Typed bridge contracts keep desktop traffic structured and evolvable as the runtime grows.",
            DesktopDirection = "Keep Electron preload thin and strongly typed so backend evolution does not leak into UI code.",
            DeliveryState = "Implemented"
        }
    ];

    internal static string NormalizeLocale(string locale)
    {
        var exact = SupportedLocales.FirstOrDefault(item =>
            string.Equals(item.Code, locale, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Code;
        }

        var language = locale.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
        var fallback = SupportedLocales.FirstOrDefault(item =>
            string.Equals(
                item.Code.Split('-', StringSplitOptions.RemoveEmptyEntries)[0],
                language,
                StringComparison.OrdinalIgnoreCase));

        return fallback?.Code ?? "en";
    }
}
