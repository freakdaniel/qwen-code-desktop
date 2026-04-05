using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Prompts;

/// <summary>
/// Provides extension members for Prompt Service Collection
/// </summary>
public static class PromptServiceCollectionExtensions
{
    /// <summary>
    /// Executes add prompt services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddPromptServices(this IServiceCollection services)
    {
        services.AddSingleton<IPromptRegistryService, PromptRegistryService>();
        return services;
    }
}
