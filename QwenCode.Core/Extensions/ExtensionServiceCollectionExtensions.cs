using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Extensions;

public static class ExtensionServiceCollectionExtensions
{
    public static IServiceCollection AddExtensionServices(this IServiceCollection services)
    {
        services.AddSingleton<IExtensionCatalogService, ExtensionCatalogService>();
        return services;
    }
}
