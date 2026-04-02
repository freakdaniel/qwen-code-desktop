using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopBootstrapProjectionService
{
    AppBootstrapPayload CreateBootstrap(string currentLocale);
}
