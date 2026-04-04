using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public static class ToolServiceCollectionExtensions
{
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
