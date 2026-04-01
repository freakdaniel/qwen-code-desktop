using System.Runtime;
using ElectronNET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QwenCode.App.AppHost;
using Serilog;
using Serilog.Events;

namespace QwenCode.App;

internal static class Program
{
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
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("QwenCode.App");

        try
        {
            logger.LogInformation("Starting Qwen Code Desktop host");
            logger.LogInformation("Content root: {ContentRoot}", AppContext.BaseDirectory);
            logger.LogInformation("Environment: {EnvironmentName}", Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production");

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
            await runtimeController.Stop().ConfigureAwait(false);
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
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
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logsDirectory, "qwen-desktop-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();
    }
}
