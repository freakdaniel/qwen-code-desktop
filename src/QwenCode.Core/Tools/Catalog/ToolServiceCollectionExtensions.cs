using Microsoft.Extensions.DependencyInjection;
using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

/// <summary>
/// Provides extension members for Tool Service Collection
/// </summary>
public static class ToolServiceCollectionExtensions
{
    /// <summary>
    /// Executes add tool services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddToolServices(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<ICronScheduler, InMemoryCronScheduler>();
        services.AddSingleton<IShellExecutionService, ShellExecutionService>();
        services.AddSingleton<IWebToolService, WebToolService>();
        services.AddSingleton<IUserQuestionToolService, UserQuestionToolService>();
        services.AddSingleton<ILspToolService, RoslynLspToolService>();
        services.AddSingleton<ISkillToolService, SkillToolService>();
        services.AddSingleton<IToolRegistry, ToolCatalogService>();
        services.AddSingleton<IToolExecutor, NativeToolHostService>();

        return services;
    }
}
