using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Ipc;

namespace QwenCode.App.AppHost;

public static class AppHostServiceCollectionExtensions
{
    public static IServiceCollection AddAppHostServices(this IServiceCollection services)
    {
        services.AddSingleton<DesktopIpcService>();

        return services;
    }
}
