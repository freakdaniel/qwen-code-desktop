using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.Core.Followup;

/// <summary>
/// Provides extension members for Followup Service Collection
/// </summary>
public static class FollowupServiceCollectionExtensions
{
    /// <summary>
    /// Executes add followup services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddFollowupServices(this IServiceCollection services)
    {
        services.AddSingleton<IFollowupSuggestionGenerator, ProviderBackedFollowupSuggestionGenerator>();
        services.AddSingleton<IFollowupSuggestionService, FollowupSuggestionService>();
        return services;
    }
}
