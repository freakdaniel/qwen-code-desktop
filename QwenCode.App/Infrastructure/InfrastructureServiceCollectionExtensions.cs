using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IDesktopEnvironmentPaths, DesktopEnvironmentPaths>();

        return services;
    }
}
