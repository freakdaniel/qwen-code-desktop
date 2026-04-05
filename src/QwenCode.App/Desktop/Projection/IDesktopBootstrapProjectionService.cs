using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Bootstrap Projection Service
/// </summary>
public interface IDesktopBootstrapProjectionService
{
    /// <summary>
    /// Creates bootstrap
    /// </summary>
    /// <param name="currentLocale">The current locale</param>
    /// <returns>The resulting app bootstrap payload</returns>
    AppBootstrapPayload CreateBootstrap(string currentLocale);
}
