using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Output;

public static class OutputServiceCollectionExtensions
{
    public static IServiceCollection AddOutputServices(this IServiceCollection services)
    {
        services.AddSingleton<TextOutputFormatter>();
        services.AddSingleton<JsonOutputFormatter>();
        services.AddSingleton<IOutputFormatter, OutputFormatter>();
        services.AddSingleton<ISessionExportService, SessionExportService>();
        return services;
    }
}
