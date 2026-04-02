using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Agents;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        services.AddSingleton<ISubagentCatalog, SubagentCatalogService>();
        services.AddSingleton<ISubagentCoordinator, SubagentCoordinatorService>();

        return services;
    }
}
