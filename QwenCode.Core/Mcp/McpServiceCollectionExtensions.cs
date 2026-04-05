using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Mcp;

public static class McpServiceCollectionExtensions
{
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
