using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Tools;

public static class ToolServiceCollectionExtensions
{
    public static IServiceCollection AddToolServices(this IServiceCollection services)
    {
        services.AddSingleton<IToolRegistry, QwenToolCatalogService>();
        services.AddSingleton<IToolExecutor, QwenNativeToolHostService>();

        return services;
    }
}
