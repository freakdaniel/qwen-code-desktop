using System.Diagnostics;
using System.Threading;
using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QwenCode.App.Desktop.DirectConnect;
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
    private static int _shutdownRequested;
    private static int _mainWindowCloseRequested;
    private static int _appHooksRegistered;

    /// <summary>
    /// Occurs when the Electron application requests the .NET host to shut down.
    /// </summary>
    public static event Action<string>? ShutdownRequested;

    /// <summary>
    /// Marks the Electron runtime as stopping so window diagnostics do not try to recover or request another shutdown.
    /// </summary>
    /// <param name="reason">The reason the runtime is stopping.</param>
    public static void NotifyRuntimeStopping(string reason)
    {
        MarkShutdownRequested(reason, notifyHost: false);
    }

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
        var directConnectState = await services.GetRequiredService<IDirectConnectServerHost>().StartAsync();
        if (directConnectState.Listening)
        {
            _logger.LogInformation("Direct-connect server listening at {BaseUrl}", directConnectState.BaseUrl);
        }
        else if (directConnectState.Enabled)
        {
            _logger.LogWarning("Direct-connect server is not listening: {Error}", directConnectState.Error);
        }

        _logger.LogInformation("Registering Desktop IPC handlers");
        services.GetRequiredService<DesktopIpcService>().RegisterAll();
        _logger.LogInformation("Desktop IPC handlers registered");

        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var indexPath = Path.Combine(wwwroot, "index.html");
        var preloadPath = Path.Combine(AppContext.BaseDirectory, "electron", "preload.js");
        var productName = configuration["DesktopShell:ProductName"] ?? "Qwen Code Desktop";

        _logger.LogInformation("Preparing renderer assets from {IndexPath}", indexPath);
        _logger.LogInformation("Preparing preload script from {PreloadPath}", preloadPath);

        if (!File.Exists(indexPath))
        {
            throw new FileNotFoundException("Renderer entrypoint was not found", indexPath);
        }

        if (!File.Exists(preloadPath))
        {
            throw new FileNotFoundException("Electron preload script was not found", preloadPath);
        }

        _indexUri = new Uri(indexPath).AbsoluteUri;
        _browserWindowOptions = CreateMainWindowOptions(productName, preloadPath);
        RegisterApplicationDiagnostics();

        await EnsureMainWindowAsync("initial-bootstrap");
        _logger.LogInformation("Main window bootstrap completed");
    }

    private static void RegisterApplicationDiagnostics()
    {
        if (Interlocked.Exchange(ref _appHooksRegistered, 1) != 0)
        {
            return;
        }

        // Let Electron finish its native quit path after the final window closes.
        // The .NET host still owns managed cleanup via BrowserWindow close events.
        ElectronApi.WindowManager.IsQuitOnWindowAllClosed = true;
    }

    private static BrowserWindowOptions CreateMainWindowOptions(string productName, string preloadPath) =>
        new()
        {
            Width = 1440,
            Height = 920,
            MinWidth = 1180,
            MinHeight = 760,
            Frame = false,
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
                "Main window closed unexpectedly during startup more than {MaxAttempts} times. Aborting recovery",
                MaxStartupRecoveryAttempts);
            RequestApplicationShutdown("startup-recovery-exhausted");
            return;
        }

        await EnsureMainWindowAsync($"startup-recovery:{reason}:{attempt}");
    }

    private static async Task EnsureMainWindowAsync(string reason)
    {
        if (_browserWindowOptions is null || string.IsNullOrWhiteSpace(_indexUri))
        {
            throw new InvalidOperationException("Main window bootstrap state has not been initialized");
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
                        _logger?.LogInformation("Main window already exists; reusing current instance for {Reason}", reason);
                        _mainWindow.Show();
                        _mainWindow.Focus();
                        return;
                    }
                }
                catch (Exception exception)
                {
                    _logger?.LogWarning(exception, "Failed while checking existing main window state before {Reason}", reason);
                }

                _mainWindow = null;
            }

            var mainWindow = await CreateWindowWhenBridgeReadyAsync(_browserWindowOptions, _indexUri, reason);
            _mainWindow = mainWindow;
            _mainWindowCreatedAtUtc = DateTimeOffset.UtcNow;
            Interlocked.Exchange(ref _mainWindowCloseRequested, 0);

            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                mainWindow.SetAutoHideMenuBar(true);
                mainWindow.RemoveMenu();
            }

            RegisterWindowDiagnostics(mainWindow);
            _logger?.LogInformation("Main window created for {IndexUri} ({Reason})", _indexUri, reason);
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
            _logger?.LogInformation("Main window #{WindowId} is ready to show", windowId);
            mainWindow.Show();
        };

        mainWindow.OnShow += () =>
        {
            _logger?.LogInformation("Main window #{WindowId} became visible", windowId);
            Interlocked.Exchange(ref _startupRecoveryAttempts, 0);
        };

        mainWindow.OnClose += () =>
        {
            _logger?.LogWarning("Main window #{WindowId} received a close event", windowId);
            Interlocked.Exchange(ref _mainWindowCloseRequested, 1);
            RequestApplicationShutdown("main-window-close");
        };

        mainWindow.OnClosed += () =>
        {
            var lifetime = DateTimeOffset.UtcNow - _mainWindowCreatedAtUtc;
            _logger?.LogInformation(
                "Main window #{WindowId} closed after {LifetimeMs} ms. Shutdown requested: {ShutdownRequested}",
                windowId,
                lifetime.TotalMilliseconds,
                IsShutdownRequested());

            _mainWindow = null;

            if (!IsShutdownRequested() &&
                Volatile.Read(ref _mainWindowCloseRequested) == 0 &&
                ShouldRecoverStartupWindow(lifetime))
            {
                _ = Task.Run(async () => await RecoverMainWindowAsync("window-closed"));
                return;
            }

            if (!IsShutdownRequested())
            {
                _logger?.LogInformation("Main window #{WindowId} closed; requesting application shutdown", windowId);
                RequestApplicationShutdown("main-window-closed");
            }
        };

        mainWindow.OnUnresponsive += () =>
        {
            _logger?.LogWarning("Main window #{WindowId} became unresponsive", windowId);
        };

        mainWindow.OnResponsive += () =>
        {
            _logger?.LogInformation("Main window #{WindowId} became responsive again", windowId);
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
                    "Electron bridge is not ready for window creation yet. Retrying attempt {Attempt} of {MaxAttempts} for {Reason}",
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

    private static void RequestApplicationShutdown(string reason)
    {
        MarkShutdownRequested(reason, notifyHost: true);
    }

    private static bool IsShutdownRequested() => Volatile.Read(ref _shutdownRequested) != 0;

    private static void MarkShutdownRequested(string reason, bool notifyHost)
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
        {
            return;
        }

        _logger?.LogInformation("Application shutdown requested by {Reason}", reason);
        if (notifyHost)
        {
            ShutdownRequested?.Invoke(reason);
        }
    }
}
