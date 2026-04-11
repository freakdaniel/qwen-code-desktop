namespace QwenCode.Core.Telemetry;

/// <summary>
/// Provides extension members for Telemetry Service Collection
/// </summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Executes add telemetry services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddTelemetryServices(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryService, TelemetryService>();
        return services;
    }
}
