using System.Diagnostics;
using ElectronNET.API.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QwenCode.App.Ipc;
using ElectronApi = ElectronNET.API.Electron;

namespace QwenCode.App.AppHost;

public static class Bootstrapper
{
    public static async Task StartAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("QwenCode.App.Bootstrapper");
        services.GetRequiredService<DesktopIpcService>().RegisterAll();

        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var indexPath = Path.Combine(wwwroot, "index.html");
        var preloadPath = Path.Combine(AppContext.BaseDirectory, "electron", "preload.js");
        var productName = configuration["DesktopShell:ProductName"] ?? "Qwen Code Desktop";

        logger.LogInformation("Preparing renderer assets from {IndexPath}", indexPath);
        logger.LogInformation("Preparing preload script from {PreloadPath}", preloadPath);

        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException("Renderer entrypoint was not found.", indexPath);
        }

        if (!File.Exists(preloadPath))
        {
            throw new FileNotFoundException("Electron preload script was not found.", preloadPath);
        }

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

        logger.LogInformation("Main window created for {IndexUri}", new Uri(indexPath).AbsoluteUri);
        mainWindow.OnReadyToShow += () => mainWindow.Show();

        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            mainWindow.SetAutoHideMenuBar(true);
            mainWindow.RemoveMenu();
        }

        logger.LogInformation("Main window bootstrap completed");
    }
}
