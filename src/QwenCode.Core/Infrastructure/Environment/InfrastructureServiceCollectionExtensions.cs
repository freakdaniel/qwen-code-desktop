using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

/// <summary>
/// Provides extension members for Infrastructure Service Collection
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Executes add infrastructure services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IDesktopEnvironmentPaths, DesktopEnvironmentPaths>();
        services.AddSingleton<IWorkspacePathResolver, WorkspacePathResolver>();
        services.AddSingleton<IGitCliService, GitCliService>();
        services.AddSingleton<IGitHistoryService, GitHistoryService>();
        services.AddSingleton<IGitWorktreeService, GitWorktreeService>();
        services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
        services.AddSingleton<IWorkspaceInspectionService, WorkspaceInspectionService>();

        return services;
    }
}
