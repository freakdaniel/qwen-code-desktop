using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Config;

public static class ConfigServiceCollectionExtensions
{
    public static IServiceCollection AddConfigServices(this IServiceCollection services)
    {
        services.AddSingleton<IConfigService, RuntimeConfigService>();
        return services;
    }
}
