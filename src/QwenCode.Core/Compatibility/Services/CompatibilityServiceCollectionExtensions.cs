using Microsoft.Extensions.DependencyInjection;
using QwenCode.Core.Models;
using QwenCode.Core.Runtime;

namespace QwenCode.Core.Compatibility;

/// <summary>
/// Provides extension members for Compatibility Service Collection
/// </summary>
public static class CompatibilityServiceCollectionExtensions
{
    /// <summary>
    /// Executes add compatibility services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddCompatibilityServices(this IServiceCollection services)
    {
        services.AddSingleton<QwenCompatibilityService>();
        services.AddSingleton<QwenRuntimeProfileService>();
        services.AddSingleton<IProjectSummaryService, ProjectSummaryService>();
        services.AddSingleton<ISettingsResolver, DesktopSettingsResolver>();

        return services;
    }
}
