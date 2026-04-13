using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime;
using InfiniFrame;
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
    [STAThread]
    private static void Main(string[] args)
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GCSettings.LatencyMode = GCLatencyMode.Interactive;
        if (AppContext.GetData("IsSingleFile") as bool? == true)
        {
            InfiniFrameSingleFileBootstrap.Initialize();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json",
                optional: true,
                reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        WriteStartupBanner();
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
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("QwenCode.App");

        try
        {
            var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var indexPath = Path.Combine(wwwrootPath, "index.html");
            if (!File.Exists(indexPath))
            {
                throw new FileNotFoundException("Renderer entrypoint was not found", indexPath);
            }

            logger.LogInformation("Starting InfiniFrame host");
            logger.LogInformation("Content root: {ContentRoot}", AppContext.BaseDirectory);
            logger.LogInformation("Renderer root: {RendererRoot}", wwwrootPath);
            logger.LogInformation("Environment: {EnvironmentName}", Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production");

            var bridge = serviceProvider.GetRequiredService<InfiniFrameDesktopBridgeService>();
            var productName = configuration["DesktopShell:ProductName"] ?? "Qwen Code Desktop";

            var window = InfiniFrameWindowBuilder.Create()
                .SetUseOsDefaultSize(false)
                .Center()
                .SetTitle(productName)
                .SetSize(new Size(1280, 720))
                .SetMinSize(new Size(1200, 720))
                .SetDevToolsEnabled(Debugger.IsAttached)
                .UseEmbeddedWwwrootAssets(
                    scheme: "app",
                    includePhysicalFallback: true,
                    physicalWwwrootPath: wwwrootPath,
                    setStartUrl: true)
                .RegisterWebMessageReceivedHandler((IInfiniFrameWindow currentWindow, string message) =>
                {
                    _ = bridge.HandleWebMessageAsync(currentWindow, message);
                })
                .Build(serviceProvider);

            bridge.Initialize(window);
            var bootstrapTask = Bootstrapper.StartAsync(serviceProvider, configuration);
            bootstrapTask.ContinueWith(
                task => logger.LogError(task.Exception, "Desktop bootstrap services failed during startup"),
                TaskContinuationOptions.OnlyOnFaulted);
            window.WaitForClose();
            bootstrapTask.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fatal error while running desktop host");
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

    private static void WriteStartupBanner()
    {
        const string logoColor = "\u001b[38;2;193;168;255m";
        const string resetColor = "\u001b[0m";

        string[] lines =
        [
            "                                       ",
            " _____               _____       _     ",
            "|     |_ _ _ ___ ___|     |___ _| |___ ",
            "|  |  | | | | -_|   |   --| . | . | -_|",
            "|__  _|_____|___|_|_|_____|___|___|___|",
            "   |__|                                "
        ];

        foreach (var line in lines)
        {
            Console.WriteLine($"{logoColor}{line}{resetColor}");
        }

        Console.WriteLine();
    }
}
