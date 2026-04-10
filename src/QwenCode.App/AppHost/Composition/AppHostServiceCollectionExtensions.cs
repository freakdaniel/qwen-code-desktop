using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Ipc;
using QwenCode.Core.Models;

namespace QwenCode.App.AppHost;

/// <summary>
/// Provides extension members for App Host Service Collection
/// </summary>
public static class AppHostServiceCollectionExtensions
{
    /// <summary>
    /// Executes add app host services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddAppHostServices(this IServiceCollection services)
    {
        services.AddSingleton<DesktopIpcService>();

        return services;
    }
}
