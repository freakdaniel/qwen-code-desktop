namespace QwenCode.Core.Hooks;

/// <summary>
/// Provides extension members for Hook Service Collection
/// </summary>
public static class HookServiceCollectionExtensions
{
    /// <summary>
    /// Executes add hook services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddHookServices(this IServiceCollection services)
    {
        services.AddSingleton<HookRegistryService>();
        services.AddSingleton<HookCommandRunner>();
        services.AddSingleton<HookOutputAggregator>();
        services.AddSingleton<IHookLifecycleService, HookLifecycleService>();
        services.AddSingleton<IUserPromptHookService, UserPromptHookService>();

        return services;
    }
}
