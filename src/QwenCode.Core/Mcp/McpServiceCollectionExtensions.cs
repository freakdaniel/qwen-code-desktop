using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.Core.Mcp;

/// <summary>
/// Provides extension members for Mcp Service Collection
/// </summary>
public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Executes add mcp services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddMcpServices(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IMcpTokenStore, FileMcpTokenStore>();
        services.AddSingleton<IMcpRegistry, McpRegistryService>();
        services.AddSingleton<IMcpConnectionManager, McpConnectionManagerService>();
        services.AddSingleton<IMcpToolRuntime, McpToolRuntimeService>();
        return services;
    }
}
