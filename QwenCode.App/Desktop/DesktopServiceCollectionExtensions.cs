using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Desktop.Diagnostics;

namespace QwenCode.App.Desktop;

public static class DesktopServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddSingleton<SourceMirrorInspectorService>();
        services.AddSingleton<RuntimePortPlannerService>();
        services.AddSingleton<IDesktopProjectionService, DesktopAppService>();

        return services;
    }
}
