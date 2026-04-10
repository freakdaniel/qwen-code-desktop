using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.Core.Extensions;

/// <summary>
/// Provides extension members for Extension Service Collection
/// </summary>
public static class ExtensionServiceCollectionExtensions
{
    /// <summary>
    /// Executes add extension services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddExtensionServices(this IServiceCollection services)
    {
        services.AddSingleton<IExtensionCatalogService, ExtensionCatalogService>();
        return services;
    }
}
