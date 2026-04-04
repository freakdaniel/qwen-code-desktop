using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Ide;

public static class IdeServiceCollectionExtensions
{
    public static IServiceCollection AddIdeServices(this IServiceCollection services)
    {
        services.AddSingleton<IIdeDetectionService, IdeDetectionService>();
        services.AddSingleton<IIdeContextService, IdeContextService>();
        services.AddSingleton<IIdeCommandRunner, IdeCommandRunner>();
        services.AddSingleton<IIdeProcessProbe, IdeProcessProbe>();
        services.AddSingleton<IIdeInstallerService, IdeInstallerService>();
        services.AddSingleton<IIdeBackendService, IdeBackendService>();
        return services;
    }
}
