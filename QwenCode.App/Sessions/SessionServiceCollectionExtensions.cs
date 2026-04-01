using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Sessions;

public static class SessionServiceCollectionExtensions
{
    public static IServiceCollection AddSessionServices(this IServiceCollection services)
    {
        services.AddSingleton<ITranscriptStore, DesktopSessionCatalogService>();
        services.AddSingleton<ISessionHost, DesktopSessionHostService>();

        return services;
    }
}
