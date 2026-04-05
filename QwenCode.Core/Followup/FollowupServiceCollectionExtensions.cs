using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Followup;

public static class FollowupServiceCollectionExtensions
{
    public static IServiceCollection AddFollowupServices(this IServiceCollection services)
    {
        services.AddSingleton<IFollowupSuggestionGenerator, ProviderBackedFollowupSuggestionGenerator>();
        services.AddSingleton<IFollowupSuggestionService, FollowupSuggestionService>();
        return services;
    }
}
