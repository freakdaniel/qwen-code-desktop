using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Hooks;

public static class HookServiceCollectionExtensions
{
    public static IServiceCollection AddHookServices(this IServiceCollection services)
    {
        services.AddSingleton<HookRegistryService>();
        services.AddSingleton<HookCommandRunner>();
        services.AddSingleton<HookOutputAggregator>();
        services.AddSingleton<IUserPromptHookService, UserPromptHookService>();

        return services;
    }
}
