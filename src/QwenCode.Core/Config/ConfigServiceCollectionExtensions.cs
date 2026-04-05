using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Config;

/// <summary>
/// Provides extension members for Config Service Collection
/// </summary>
public static class ConfigServiceCollectionExtensions
{
    /// <summary>
    /// Executes add config services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddConfigServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigService, RuntimeConfigService>();
        return services;
    }
}
