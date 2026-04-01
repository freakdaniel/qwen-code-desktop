using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Compatibility;
using QwenCode.App.Desktop;
using QwenCode.App.Ipc;
using QwenCode.App.Infrastructure;
using QwenCode.App.Options;
using QwenCode.App.Permissions;
using QwenCode.App.Runtime;
using QwenCode.App.Sessions;
using QwenCode.App.Tools;

namespace QwenCode.App.AppHost;

public static class DesktopShellServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopShellServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DesktopShellOptions>()
            .Bind(configuration.GetSection(DesktopShellOptions.SectionName));

        services
            .AddInfrastructureServices()
            .AddCompatibilityServices()
            .AddPermissionServices()
            .AddRuntimeServices()
            .AddToolServices()
            .AddSessionServices()
            .AddDesktopServices()
            .AddAppHostServices();

        return services;
    }
}
