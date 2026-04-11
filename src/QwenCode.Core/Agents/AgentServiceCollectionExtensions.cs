namespace QwenCode.Core.Agents;

/// <summary>
/// Provides extension members for Agent Service Collection
/// </summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Executes add agent services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
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
