using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Compatibility;

public static class CompatibilityServiceCollectionExtensions
{
    public static IServiceCollection AddCompatibilityServices(this IServiceCollection services)
    {
        services.AddSingleton<QwenCompatibilityService>();
        services.AddSingleton<QwenRuntimeProfileService>();
        services.AddSingleton<ISettingsResolver, DesktopSettingsResolver>();

        return services;
    }
}
