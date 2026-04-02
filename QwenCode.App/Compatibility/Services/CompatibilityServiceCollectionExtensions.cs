using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Compatibility;

public static class CompatibilityServiceCollectionExtensions
{
    public static IServiceCollection AddCompatibilityServices(this IServiceCollection services)
    {
        services.AddSingleton<QwenCompatibilityService>();
        services.AddSingleton<QwenRuntimeProfileService>();
        services.AddSingleton<IProjectSummaryService, ProjectSummaryService>();
        services.AddSingleton<ISettingsResolver, DesktopSettingsResolver>();

        return services;
    }
}
