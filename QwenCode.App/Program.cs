using System.Runtime;
using ElectronNET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Ipc;
using QwenCode.App.Options;
using QwenCode.App.Services;

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

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<DesktopShellOptions>()
            .Bind(configuration.GetSection(DesktopShellOptions.SectionName));
        services.AddSingleton<DesktopAppService>();
        services.AddSingleton<DesktopIpcService>();

        using var serviceProvider = services.BuildServiceProvider();
        var runtimeController = ElectronNetRuntime.RuntimeController;

        try
        {
            ElectronNetRuntime.ElectronExtraArguments = string.Join(
                " ",
                "--disable-background-networking",
                "--disable-dev-shm-usage",
                "--disable-renderer-backgrounding");

            await runtimeController.Start();
            await runtimeController.WaitReadyTask;

            await Bootstrapper.StartAsync(serviceProvider, configuration);

            await runtimeController.WaitStoppedTask;
        }
        catch
        {
            await runtimeController.Stop().ConfigureAwait(false);
            throw;
        }
    }
}
