using Microsoft.Extensions.Options;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

public sealed class LocaleStateService(IOptions<DesktopShellOptions> options) : ILocaleStateService
{
    private readonly object _syncRoot = new();
    private string _currentLocale = DesktopProjectionCatalog.NormalizeLocale(options.Value.DefaultLocale);

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
