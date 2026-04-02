using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IDesktopEnvironmentPaths, DesktopEnvironmentPaths>();
        services.AddSingleton<IWorkspacePathResolver, WorkspacePathResolver>();

        return services;
    }
}
