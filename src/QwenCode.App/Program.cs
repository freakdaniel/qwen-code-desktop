using System.Runtime;
using System.Threading;
using System.Diagnostics;
using ElectronNET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QwenCode.App.AppHost;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using QwenCode.Core.Models;

namespace QwenCode.App;

internal static class Program
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);
    private static int _shutdownInitiated;
    private static int _runtimeStopped;
    private static int _processCleanupCompleted;
    private static int _consoleCancelCount;

    private static async Task Main(string[] args)
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GCSettings.LatencyMode = GCLatencyMode.Interactive;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json",
                optional: true,
                reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = CreateLogger(configuration);
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });
        services.AddDesktopShellServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        ConsoleLogBridge.Install(loggerFactory);
        var runtimeController = ElectronNetRuntime.RuntimeController;
        var logger = loggerFactory.CreateLogger("QwenCode.App");
        Bootstrapper.ShutdownRequested += reason => _ = RequestShutdownAsync(runtimeController, logger, reason);
        RegisterShutdownHandlers(runtimeController, logger);

        try
        {
            logger.LogInformation("Starting Qwen Code Desktop host");
            logger.LogInformation("Content root: {ContentRoot}", AppContext.BaseDirectory);
            logger.LogInformation("Environment: {EnvironmentName}", Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production");
            ElectronProcessJanitor.CleanupStaleUnpackedHosts(logger);

            ElectronNetRuntime.ElectronExtraArguments = string.Join(
                " ",
                "--disable-background-networking",
                "--disable-dev-shm-usage",
                "--disable-renderer-backgrounding");

            logger.LogInformation("Starting Electron.NET runtime");
            await runtimeController.Start();
            await runtimeController.WaitReadyTask;
            logger.LogInformation("Electron.NET runtime ready");

            await Bootstrapper.StartAsync(serviceProvider, configuration);
            logger.LogInformation("Bootstrap completed; waiting for runtime stop");

            await runtimeController.WaitStoppedTask;
            Interlocked.Exchange(ref _runtimeStopped, 1);
            logger.LogInformation("Electron.NET runtime stopped");
            RunProcessCleanupOnce(logger, "runtime-stopped");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fatal error while running desktop host");
            await StopRuntimeAsync(runtimeController, logger, "fatal-error").ConfigureAwait(false);
            RunProcessCleanupOnce(logger, "fatal-error");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void RegisterShutdownHandlers(object runtimeController, Microsoft.Extensions.Logging.ILogger logger)
    {
        Console.CancelKeyPress += async (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            var cancelCount = Interlocked.Increment(ref _consoleCancelCount);

            if (Volatile.Read(ref _shutdownInitiated) != 0 || cancelCount > 1)
            {
                logger.LogWarning("Console cancel requested again during shutdown. Force terminating Electron host");
                ForceTerminateCurrentProcess(logger, "console-cancel-repeat");
                return;
            }

            logger.LogWarning("Console cancel requested. Shutting down Electron host");
            await RequestShutdownAsync(runtimeController, logger, "console-cancel");
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            RunProcessCleanupOnce(logger, "process-exit");
        };
    }

    private static async Task RequestShutdownAsync(object runtimeController, Microsoft.Extensions.Logging.ILogger logger, string reason)
    {
        if (Interlocked.Exchange(ref _shutdownInitiated, 1) != 0)
        {
            return;
        }

        Bootstrapper.NotifyRuntimeStopping(reason);
        await StopRuntimeAsync(runtimeController, logger, reason).ConfigureAwait(false);
        RunProcessCleanupOnce(logger, reason);
    }

    private static async Task StopRuntimeAsync(object runtimeController, Microsoft.Extensions.Logging.ILogger logger, string reason)
    {
        if (Volatile.Read(ref _runtimeStopped) != 0)
        {
            return;
        }

        if (TryMarkRuntimeStoppedWithoutStopping(runtimeController, logger, reason))
        {
            return;
        }

        try
        {
            logger.LogInformation("Stopping Electron.NET runtime due to {Reason}", reason);
            var stopTask = Task.Run(async () =>
            {
                Task runtimeStopTask = InvokeRuntimeStopAsync(runtimeController);
                await runtimeStopTask.ConfigureAwait(false);
            });

            var completedTask = await Task.WhenAny(stopTask, Task.Delay(ShutdownTimeout)).ConfigureAwait(false);
            if (completedTask != stopTask)
            {
                logger.LogError("Electron.NET runtime stop timed out after {TimeoutMs} ms during {Reason}", ShutdownTimeout.TotalMilliseconds, reason);
                ForceTerminateCurrentProcess(logger, $"{reason}-timeout");
                return;
            }

            await stopTask.ConfigureAwait(false);
            Interlocked.Exchange(ref _runtimeStopped, 1);
        }
        catch (Exception exception)
        {
            if (IsBenignRuntimeStopException(exception) || TryMarkRuntimeStoppedWithoutStopping(runtimeController, logger, reason))
            {
                Interlocked.Exchange(ref _runtimeStopped, 1);
                logger.LogInformation(
                    "Electron.NET runtime was already stopping or stopped during {Reason}; treating shutdown as complete",
                    reason);
                return;
            }

            logger.LogWarning(
                exception,
                "Graceful Electron.NET runtime stop failed during {Reason}. Falling back to process cleanup",
                reason);
        }
    }

    private static bool TryMarkRuntimeStoppedWithoutStopping(object runtimeController, Microsoft.Extensions.Logging.ILogger logger, string reason)
    {
        try
        {
            var waitStoppedTask = TryGetTaskProperty(runtimeController, "WaitStoppedTask");
            if (waitStoppedTask?.IsCompleted == true)
            {
                Interlocked.Exchange(ref _runtimeStopped, 1);
                logger.LogDebug("Skipping runtime stop during {Reason} because WaitStoppedTask is already completed", reason);
                return true;
            }

            var state = TryGetRuntimeState(runtimeController);
            if (!string.IsNullOrWhiteSpace(state))
            {
                if (string.Equals(state, "Stopped", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Exchange(ref _runtimeStopped, 1);
                    logger.LogDebug("Skipping runtime stop during {Reason} because runtime state is {State}", reason, state);
                    return true;
                }

                if (string.Equals(state, "Stopping", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Skipping runtime stop during {Reason} because runtime state is {State}", reason, state);
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Failed to inspect Electron.NET runtime state during {Reason}", reason);
        }

        return false;
    }

    private static Task InvokeRuntimeStopAsync(object runtimeController)
    {
        var stopMethod = runtimeController.GetType().GetMethod("Stop", Type.EmptyTypes);
        return stopMethod?.Invoke(runtimeController, null) as Task
            ?? throw new InvalidOperationException("Electron.NET runtime controller does not expose Stop().");
    }

    private static Task? TryGetTaskProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        return property?.GetValue(target) as Task;
    }

    private static string? TryGetRuntimeState(object runtimeController)
    {
        foreach (var propertyName in new[] { "State", "CurrentState", "LifetimeState" })
        {
            var property = runtimeController.GetType().GetProperty(propertyName);
            var value = property?.GetValue(runtimeController);
            if (value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static bool IsBenignRuntimeStopException(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("Invalid state transition from Stopped to Stopping", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Invalid state transition from Stopping to Stopping", StringComparison.OrdinalIgnoreCase);
    }

    private static void RunProcessCleanupOnce(Microsoft.Extensions.Logging.ILogger logger, string reason)
    {
        if (Interlocked.Exchange(ref _processCleanupCompleted, 1) == 0)
        {
            ElectronProcessJanitor.CleanupCurrentUnpackedHost(logger, reason);
        }
    }

    private static void ForceTerminateCurrentProcess(Microsoft.Extensions.Logging.ILogger logger, string reason)
    {
        try
        {
            RunProcessCleanupOnce(logger, reason);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to clean up Electron host before forced termination");
        }

        try
        {
            logger.LogWarning("Force terminating current desktop host process due to {Reason}", reason);
            Log.CloseAndFlush();
        }
        finally
        {
            Process.GetCurrentProcess().Kill(entireProcessTree: true);
        }
    }

    private static Serilog.ILogger CreateLogger(IConfiguration configuration)
    {
        var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);

        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                theme: AnsiConsoleTheme.Code,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logsDirectory, "qwen-desktop-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();
    }
}
