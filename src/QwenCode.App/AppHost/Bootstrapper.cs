using InfiniFrame;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QwenCode.App.Desktop.DirectConnect;

namespace QwenCode.App.AppHost;

/// <summary>
/// Coordinates application startup for the InfiniFrame desktop shell.
/// </summary>
public static class Bootstrapper
{
    /// <summary>
    /// Starts desktop services that depend on the native window.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task StartAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("QwenCode.App.Bootstrapper");
        var directConnectState = await services.GetRequiredService<IDirectConnectServerHost>().StartAsync();
        if (directConnectState.Listening)
        {
            logger.LogInformation("Direct-connect server listening at {BaseUrl}", directConnectState.BaseUrl);
        }
        else if (directConnectState.Enabled)
        {
            logger.LogWarning("Direct-connect server is not listening: {Error}", directConnectState.Error);
        }

        logger.LogInformation("Desktop bootstrap services initialized");
    }
}
