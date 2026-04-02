using System.Runtime;
using System.Threading;
using ElectronNET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QwenCode.App.AppHost;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using QwenCode.App.Models;

namespace QwenCode.App;

internal static class Program
{
    private static int _shutdownInitiated;

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
        var runtimeController = ElectronNetRuntime.RuntimeController;
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        ConsoleLogBridge.Install(loggerFactory);
        var logger = loggerFactory.CreateLogger("QwenCode.App");
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
            logger.LogInformation("Electron.NET runtime stopped");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fatal error while running desktop host");
            try
            {
                await runtimeController.Stop().ConfigureAwait(false);
            }
            catch (Exception stopException)
            {
                logger.LogWarning(stopException, "Electron.NET runtime stop was skipped because the runtime was already stopping or stopped.");
            }
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void RegisterShutdownHandlers(dynamic runtimeController, Microsoft.Extensions.Logging.ILogger logger)
    {
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            logger.LogWarning("Console cancel requested. Shutting down Electron host.");
            eventArgs.Cancel = true;
            RequestShutdown(runtimeController, logger, "console-cancel");
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            RequestShutdown(runtimeController, logger, "process-exit");
        };
    }

    private static void RequestShutdown(dynamic runtimeController, Microsoft.Extensions.Logging.ILogger logger, string reason)
    {
        if (Interlocked.Exchange(ref _shutdownInitiated, 1) != 0)
        {
            return;
        }

        try
        {
            logger.LogInformation("Stopping Electron.NET runtime due to {Reason}.", reason);
            runtimeController.Stop().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Graceful Electron.NET runtime stop failed during {Reason}. Falling back to process cleanup.",
                reason);
        }
        finally
        {
            ElectronProcessJanitor.CleanupCurrentUnpackedHost(logger, reason);
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
