using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Desktop.DirectConnect;
using QwenCode.App.Desktop.Projection;
using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Provides extension members for Desktop Service Collection
/// </summary>
public static class DesktopServiceCollectionExtensions
{
    /// <summary>
    /// Executes add desktop services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddSingleton<ILocaleStateService, LocaleStateService>();
        services.AddSingleton<IDesktopBootstrapProjectionService, BootstrapProjectionService>();
        services.AddSingleton<IDesktopArenaProjectionService, ArenaProjectionService>();
        services.AddSingleton<IDesktopAuthProjectionService, AuthProjectionService>();
        services.AddSingleton<IDesktopChannelProjectionService, ChannelProjectionService>();
        services.AddSingleton<IDesktopMcpProjectionService, McpProjectionService>();
        services.AddSingleton<IDesktopPromptProjectionService, PromptProjectionService>();
        services.AddSingleton<IDesktopMcpResourceProjectionService, McpResourceProjectionService>();
        services.AddSingleton<IDesktopFollowupProjectionService, FollowupProjectionService>();
        services.AddSingleton<IDesktopExtensionProjectionService, ExtensionProjectionService>();
        services.AddSingleton<IDesktopWorkspaceProjectionService, WorkspaceProjectionService>();
        services.AddSingleton<IDesktopSessionProjectionService, SessionProjectionService>();
        services.AddSingleton<ISessionEventPublisher>(sp =>
            (ISessionEventPublisher)sp.GetRequiredService<IDesktopSessionProjectionService>());
        services.AddSingleton<IDirectConnectSessionService, DirectConnectSessionService>();
        services.AddSingleton<IDirectConnectServerHost, DirectConnectHttpServerHost>();
        services.AddSingleton<ISessionTitleGenerationService, SessionTitleGenerationService>();
        services.AddSingleton<IDesktopProjectionService, DesktopAppService>();

        return services;
    }
}
