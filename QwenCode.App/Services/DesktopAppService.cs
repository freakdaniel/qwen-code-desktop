using Microsoft.Extensions.Options;
using QwenCode.App.Enums;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Services;

public sealed class DesktopAppService(IOptions<DesktopShellOptions> options)
{
    private static readonly IReadOnlyList<LocaleOption> SupportedLocales =
    [
        new() { Code = "en", Name = "English", NativeName = "English" },
        new() { Code = "ru", Name = "Russian", NativeName = "\u0420\u0443\u0441\u0441\u043a\u0438\u0439" },
        new() { Code = "zh-CN", Name = "Chinese", NativeName = "\u7b80\u4f53\u4e2d\u6587" },
        new() { Code = "de", Name = "German", NativeName = "Deutsch" },
        new() { Code = "fr", Name = "French", NativeName = "Francais" },
        new() { Code = "es", Name = "Spanish", NativeName = "Espanol" },
        new() { Code = "ja", Name = "Japanese", NativeName = "\u65e5\u672c\u8a9e" },
        new() { Code = "ko", Name = "Korean", NativeName = "\ud55c\uad6d\uc5b4" },
        new() { Code = "pt-BR", Name = "Portuguese (Brazil)", NativeName = "Portugu\u00eas (Brasil)" },
        new() { Code = "tr", Name = "Turkish", NativeName = "T\u00fcrk\u00e7e" },
        new() { Code = "ar", Name = "Arabic", NativeName = "\u0627\u0644\u0639\u0631\u0628\u064a\u0629" }
    ];

    private static readonly IReadOnlyList<ResearchTrack> Tracks =
    [
        new()
        {
            Title = "Keep qwen-core authoritative",
            Summary = "The desktop host should wrap qwen runtime instead of replacing its execution model."
        },
        new()
        {
            Title = "Adopt Claude-grade tool UX",
            Summary = "Tool, task, and approval flows should be structured explicitly in the renderer."
        },
        new()
        {
            Title = "Use HyPrism IPC discipline",
            Summary = "Typed preload bridges keep Electron integration narrow, testable, and maintainable."
        }
    ];

    private static readonly IReadOnlyList<string> CompatibilityGoals =
    [
        "Do not fork .qwen settings, memory, or agent formats.",
        "Preserve qwen session and history compatibility before UI expansion.",
        "Keep runtime logic in qwen and native shell orchestration in .NET.",
        "Treat Claude as a UX reference, not as a provider dependency."
    ];

    private readonly object _syncRoot = new();
    private readonly DesktopShellOptions _options = options.Value;
    private DesktopMode _currentMode = options.Value.DefaultMode;
    private string _currentLocale = NormalizeLocale(options.Value.DefaultLocale);

    public event EventHandler<DesktopStateChangedEvent>? StateChanged;

    public Task<AppBootstrapPayload> GetBootstrapAsync()
    {
        lock (_syncRoot)
        {
            return Task.FromResult(new AppBootstrapPayload
            {
                ProductName = _options.ProductName,
                CurrentMode = _currentMode,
                CurrentLocale = _currentLocale,
                Locales = SupportedLocales,
                Sources = _options.Sources,
                Tracks = Tracks,
                CompatibilityGoals = CompatibilityGoals
            });
        }
    }

    public Task<DesktopStateChangedEvent> SetModeAsync(DesktopMode mode)
    {
        DesktopStateChangedEvent state;
        lock (_syncRoot)
        {
            _currentMode = mode;
            state = Snapshot();
        }

        StateChanged?.Invoke(this, state);
        return Task.FromResult(state);
    }

    public Task<DesktopStateChangedEvent> SetLocaleAsync(string locale)
    {
        DesktopStateChangedEvent state;
        lock (_syncRoot)
        {
            _currentLocale = NormalizeLocale(locale);
            state = Snapshot();
        }

        StateChanged?.Invoke(this, state);
        return Task.FromResult(state);
    }

    private DesktopStateChangedEvent Snapshot() => new()
    {
        CurrentMode = _currentMode,
        CurrentLocale = _currentLocale,
        TimestampUtc = DateTime.UtcNow
    };

    private static string NormalizeLocale(string locale)
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
