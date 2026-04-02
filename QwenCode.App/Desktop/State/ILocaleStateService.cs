using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface ILocaleStateService
{
    string CurrentLocale { get; }

    DesktopStateChangedEvent SetLocale(string locale);
}
