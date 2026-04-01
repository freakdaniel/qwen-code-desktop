using System.Diagnostics;
using ElectronNET.API.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Ipc;
using ElectronApi = ElectronNET.API.Electron;

namespace QwenCode.App;

public static class Bootstrapper
{
    public static async Task StartAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        services.GetRequiredService<DesktopIpcService>().RegisterAll();

        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var indexPath = Path.Combine(wwwroot, "index.html");
        var preloadPath = Path.Combine(AppContext.BaseDirectory, "electron", "preload.js");
        var productName = configuration["DesktopShell:ProductName"] ?? "Qwen Code Desktop";

        var mainWindow = await ElectronApi.WindowManager.CreateWindowAsync(
            new BrowserWindowOptions
            {
                Width = 1440,
                Height = 920,
                MinWidth = 1180,
                MinHeight = 760,
                Frame = true,
                Show = false,
                Center = true,
                Title = productName,
                BackgroundColor = "#111117",
                WebPreferences = new WebPreferences
                {
                    Preload = preloadPath,
                    ContextIsolation = true,
                    NodeIntegration = false,
                    DevTools = Debugger.IsAttached,
                    WebviewTag = false,
                    Webgl = true
                }
            },
            new Uri(indexPath).AbsoluteUri);

        mainWindow.OnReadyToShow += () => mainWindow.Show();

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            mainWindow.SetAutoHideMenuBar(true);
            mainWindow.RemoveMenu();
        }
    }
}
