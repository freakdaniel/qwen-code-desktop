using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Agents;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        services.AddSingleton<IArenaSessionRegistry, ArenaSessionRegistry>();
        services.AddSingleton<ISubagentModelSelectionService, SubagentModelSelectionService>();
        services.AddSingleton<ISubagentValidationService, SubagentValidationService>();
        services.AddSingleton<ISubagentCatalog, SubagentCatalogService>();
        services.AddSingleton<ISubagentCoordinator, SubagentCoordinatorService>();
        services.AddSingleton<IAgentArenaService, AgentArenaService>();

        return services;
    }
}
