using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Locale State Service
/// </summary>
public interface ILocaleStateService
{
    /// <summary>
    /// Gets the current locale
    /// </summary>
    string CurrentLocale { get; }

    /// <summary>
    /// Sets locale
    /// </summary>
    /// <param name="locale">The locale</param>
    /// <returns>The resulting desktop state changed event</returns>
    DesktopStateChangedEvent SetLocale(string locale);
}
