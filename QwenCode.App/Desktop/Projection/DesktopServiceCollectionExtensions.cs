using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public static class DesktopServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddSingleton<ILocaleStateService, LocaleStateService>();
        services.AddSingleton<IDesktopBootstrapProjectionService, BootstrapProjectionService>();
        services.AddSingleton<IDesktopAuthProjectionService, AuthProjectionService>();
        services.AddSingleton<IDesktopChannelProjectionService, ChannelProjectionService>();
        services.AddSingleton<IDesktopMcpProjectionService, McpProjectionService>();
        services.AddSingleton<IDesktopExtensionProjectionService, ExtensionProjectionService>();
        services.AddSingleton<IDesktopWorkspaceProjectionService, WorkspaceProjectionService>();
        services.AddSingleton<IDesktopSessionProjectionService, SessionProjectionService>();
        services.AddSingleton<IDesktopProjectionService, DesktopAppService>();

        return services;
    }
}
