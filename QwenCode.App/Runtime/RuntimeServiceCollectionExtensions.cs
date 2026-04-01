using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Runtime;

public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeServices(this IServiceCollection services)
    {
        services.AddSingleton<ISlashCommandRuntime, QwenSlashCommandRuntime>();
        services.AddSingleton<ICommandActionRuntime, QwenCommandActionRuntime>();

        return services;
    }
}
