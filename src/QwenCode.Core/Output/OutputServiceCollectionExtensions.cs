using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.Core.Output;

/// <summary>
/// Provides extension members for Output Service Collection
/// </summary>
public static class OutputServiceCollectionExtensions
{
    /// <summary>
    /// Executes add output services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddOutputServices(this IServiceCollection services)
    {
        services.AddSingleton<TextOutputFormatter>();
        services.AddSingleton<JsonOutputFormatter>();
        services.AddSingleton<IOutputFormatter, OutputFormatter>();
        services.AddSingleton<ISessionExportService, SessionExportService>();
        return services;
    }
}
