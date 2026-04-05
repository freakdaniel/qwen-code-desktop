using System.Diagnostics;
using System.Threading;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QwenCode.App.Ipc;
using ElectronApi = ElectronNET.API.Electron;

namespace QwenCode.App.AppHost;

/// <summary>
/// Represents the Bootstrapper
/// </summary>
public static class Bootstrapper
{
    private static readonly SemaphoreSlim WindowSync = new(1, 1);
    private static readonly TimeSpan StartupRecoveryWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BridgeRetryDelay = TimeSpan.FromMilliseconds(250);
    private const int MaxStartupRecoveryAttempts = 2;
    private const int MaxBridgeReadyAttempts = 20;

    private static BrowserWindow? _mainWindow;
    private static ILogger? _logger;
    private static string? _indexUri;
    private static BrowserWindowOptions? _browserWindowOptions;
    private static DateTimeOffset _mainWindowCreatedAtUtc;
    private static int _startupRecoveryAttempts;
    private static bool _shutdownRequested;

    /// <summary>
    /// Starts async
    /// </summary>
    /// <param name="services">The services</param>
    /// <param name="configuration">The configuration to apply</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public static async Task StartAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        _logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("QwenCode.App.Bootstrapper");
        services.GetRequiredService<DesktopIpcService>().RegisterAll();

        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var indexPath = Path.Combine(wwwroot, "index.html");
        var preloadPath = Path.Combine(AppContext.BaseDirectory, "electron", "preload.js");
        var productName = configuration["DesktopShell:ProductName"] ?? "Qwen Code Desktop";

        _logger.LogInformation("Preparing renderer assets from {IndexPath}", indexPath);
        _logger.LogInformation("Preparing preload script from {PreloadPath}", preloadPath);

        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException("Renderer entrypoint was not found.", indexPath);
        }

        if (!File.Exists(preloadPath))
        {
            throw new FileNotFoundException("Electron preload script was not found.", preloadPath);
        }

        _indexUri = new Uri(indexPath).AbsoluteUri;
        _browserWindowOptions = CreateMainWindowOptions(productName, preloadPath);

        await EnsureMainWindowAsync("initial-bootstrap");
        _logger.LogInformation("Main window bootstrap completed");
    }

    private static BrowserWindowOptions CreateMainWindowOptions(string productName, string preloadPath) =>
        new()
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
        };

    private static async Task RecoverMainWindowAsync(string reason)
    {
        var attempt = Interlocked.Increment(ref _startupRecoveryAttempts);
        if (attempt > MaxStartupRecoveryAttempts)
        {
            _logger?.LogError(
                "Main window closed unexpectedly during startup more than {MaxAttempts} times. Aborting recovery.",
                MaxStartupRecoveryAttempts);
            _shutdownRequested = true;
            ElectronApi.App.Quit();
            return;
        }

        await EnsureMainWindowAsync($"startup-recovery:{reason}:{attempt}");
    }

    private static async Task EnsureMainWindowAsync(string reason)
    {
        if (_browserWindowOptions is null || string.IsNullOrWhiteSpace(_indexUri))
        {
            throw new InvalidOperationException("Main window bootstrap state has not been initialized.");
        }

        await WindowSync.WaitAsync();
        try
        {
            if (_mainWindow is not null)
            {
                try
                {
                    if (!await _mainWindow.IsDestroyedAsync())
                    {
                        _logger?.LogInformation("Main window already exists; reusing current instance for {Reason}.", reason);
                        _mainWindow.Show();
                        _mainWindow.Focus();
                        return;
                    }
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, "Failed while checking existing main window state before {Reason}.", reason);
                }

                _mainWindow = null;
            }

            var mainWindow = await CreateWindowWhenBridgeReadyAsync(_browserWindowOptions, _indexUri, reason);
            _mainWindow = mainWindow;
            _mainWindowCreatedAtUtc = DateTimeOffset.UtcNow;

            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                mainWindow.SetAutoHideMenuBar(true);
                mainWindow.RemoveMenu();
            }

            RegisterWindowDiagnostics(mainWindow);
            _logger?.LogInformation("Main window created for {IndexUri} ({Reason}).", _indexUri, reason);
        }
        finally
        {
            WindowSync.Release();
        }
    }

    private static void RegisterWindowDiagnostics(BrowserWindow mainWindow)
    {
        var windowId = mainWindow.Id;

        mainWindow.OnReadyToShow += () =>
        {
            _logger?.LogInformation("Main window #{WindowId} is ready to show.", windowId);
            mainWindow.Show();
        };

        mainWindow.OnShow += () =>
        {
            _logger?.LogInformation("Main window #{WindowId} became visible.", windowId);
            Interlocked.Exchange(ref _startupRecoveryAttempts, 0);
        };

        mainWindow.OnClose += () =>
        {
            _logger?.LogWarning("Main window #{WindowId} received a close event.", windowId);
        };

        mainWindow.OnClosed += () =>
        {
            var lifetime = DateTimeOffset.UtcNow - _mainWindowCreatedAtUtc;
            _logger?.LogWarning(
                "Main window #{WindowId} closed after {LifetimeMs} ms. Shutdown requested: {ShutdownRequested}.",
                windowId,
                lifetime.TotalMilliseconds,
                _shutdownRequested);

            _mainWindow = null;

            if (!_shutdownRequested && ShouldRecoverStartupWindow(lifetime))
            {
                _ = Task.Run(async () => await RecoverMainWindowAsync("window-closed"));
                return;
            }

            if (!_shutdownRequested)
            {
                _shutdownRequested = true;
                _logger?.LogInformation("Main window #{WindowId} closed outside startup recovery window; quitting application.", windowId);
                ElectronApi.App.Quit();
            }
        };

        mainWindow.OnUnresponsive += () =>
        {
            _logger?.LogWarning("Main window #{WindowId} became unresponsive.", windowId);
        };

        mainWindow.OnResponsive += () =>
        {
            _logger?.LogInformation("Main window #{WindowId} became responsive again.", windowId);
        };
    }

    private static bool ShouldRecoverStartupWindow() =>
        ShouldRecoverStartupWindow(DateTimeOffset.UtcNow - _mainWindowCreatedAtUtc);

    private static bool ShouldRecoverStartupWindow(TimeSpan lifetime) =>
        lifetime <= StartupRecoveryWindow && _startupRecoveryAttempts < MaxStartupRecoveryAttempts;

    private static async Task<BrowserWindow> CreateWindowWhenBridgeReadyAsync(
        BrowserWindowOptions browserWindowOptions,
        string indexUri,
        string reason)
    {
        for (var attempt = 1; attempt <= MaxBridgeReadyAttempts; attempt++)
        {
            try
            {
                return await ElectronApi.WindowManager.CreateWindowAsync(browserWindowOptions, indexUri);
            }
            catch (Exception exception) when (IsBridgeNotReadyException(exception) && attempt < MaxBridgeReadyAttempts)
            {
                _logger?.LogWarning(
                    exception,
                    "Electron bridge is not ready for window creation yet. Retrying attempt {Attempt} of {MaxAttempts} for {Reason}.",
                    attempt,
                    MaxBridgeReadyAttempts,
                    reason);
                await Task.Delay(BridgeRetryDelay);
            }
        }

        return await ElectronApi.WindowManager.CreateWindowAsync(browserWindowOptions, indexUri);
    }

    private static bool IsBridgeNotReadyException(Exception exception) =>
        exception.Message.Contains("Cannot access socket bridge", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Runtime is not in 'Ready' state", StringComparison.OrdinalIgnoreCase);
}
