using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Prompts;

public static class PromptServiceCollectionExtensions
{
    public static IServiceCollection AddPromptServices(this IServiceCollection services)
    {
        services.AddSingleton<IPromptRegistryService, PromptRegistryService>();
        return services;
    }
}
