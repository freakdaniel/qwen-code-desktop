using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public static class ToolServiceCollectionExtensions
{
    public static IServiceCollection AddToolServices(this IServiceCollection services)
    {
        services.AddSingleton<IToolRegistry, ToolCatalogService>();
        services.AddSingleton<IToolExecutor, NativeToolHostService>();

        return services;
    }
}
