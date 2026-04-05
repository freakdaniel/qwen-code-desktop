using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Telemetry;

public static class TelemetryServiceCollectionExtensions
{
    public static IServiceCollection AddTelemetryServices(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryService, TelemetryService>();
        return services;
    }
}
