using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace QwenCode.App.Channels;

/// <summary>
/// Provides extension members for Channel Service Collection
/// </summary>
public static class ChannelServiceCollectionExtensions
{
    /// <summary>
    /// Executes add channel services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddChannelServices(this IServiceCollection services)
    {
        services.TryAddSingleton<HttpClient>();
        services.AddSingleton<IChannelRegistryService, ChannelRegistryService>();
        services.AddSingleton<IChannelPluginRegistryService, ChannelPluginRegistryService>();
        services.AddSingleton<IChannelPluginRuntimeService, ChannelPluginRuntimeService>();
        services.AddSingleton<IChannelSessionRouter, ChannelSessionRouterService>();
        services.AddSingleton<IChannelAdapter, TelegramChannelAdapter>();
        services.AddSingleton<IChannelAdapter, WeixinChannelAdapter>();
        services.AddSingleton<IChannelAdapter, DingtalkChannelAdapter>();
        services.AddSingleton<IChannelRuntimeService, ChannelRuntimeService>();
        services.AddSingleton<IChannelDeliveryService, ChannelDeliveryService>();
        return services;
    }
}
