using Microsoft.Extensions.Options;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Locale State Service
/// </summary>
/// <param name="options">The options</param>
public sealed class LocaleStateService(IOptions<DesktopShellOptions> options) : ILocaleStateService
{
    private readonly object _syncRoot = new();
    private string _currentLocale = DesktopProjectionCatalog.NormalizeLocale(options.Value.DefaultLocale);

    /// <summary>
    /// Gets the current locale
    /// </summary>
    public string CurrentLocale
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentLocale;
            }
        }
    }

    /// <summary>
    /// Sets locale
    /// </summary>
    /// <param name="locale">The locale</param>
    /// <returns>The resulting desktop state changed event</returns>
    public DesktopStateChangedEvent SetLocale(string locale)
    {
        lock (_syncRoot)
        {
            _currentLocale = DesktopProjectionCatalog.NormalizeLocale(locale);
            return new DesktopStateChangedEvent
            {
                CurrentMode = DesktopMode.Code,
                CurrentLocale = _currentLocale,
                TimestampUtc = DateTime.UtcNow
            };
        }
    }
}
