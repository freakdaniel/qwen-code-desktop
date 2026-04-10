using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.Core.Ide;

/// <summary>
/// Provides extension members for Ide Service Collection
/// </summary>
public static class IdeServiceCollectionExtensions
{
    /// <summary>
    /// Executes add ide services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddIdeServices(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IIdeDetectionService, IdeDetectionService>();
        services.AddSingleton<IIdeContextService, IdeContextService>();
        services.AddSingleton<IIdeCommandRunner, IdeCommandRunner>();
        services.AddSingleton<IIdeProcessProbe, IdeProcessProbe>();
        services.AddSingleton<IIdeInstallerService, IdeInstallerService>();
        services.AddSingleton<IIdeBackendService, IdeBackendService>();
        services.AddSingleton<IIdeCompanionTransport, IdeCompanionTransport>();
        services.AddSingleton<IIdeClientService, IdeClientService>();
        return services;
    }
}
