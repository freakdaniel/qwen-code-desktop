using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Channels;

public static class ChannelServiceCollectionExtensions
{
    public static IServiceCollection AddChannelServices(this IServiceCollection services)
    {
        services.AddSingleton<IChannelRegistryService, ChannelRegistryService>();
        services.AddSingleton<IChannelSessionRouter, ChannelSessionRouterService>();
        services.AddSingleton<IChannelAdapter, TelegramChannelAdapter>();
        services.AddSingleton<IChannelAdapter, WeixinChannelAdapter>();
        services.AddSingleton<IChannelAdapter, DingtalkChannelAdapter>();
        services.AddSingleton<IChannelRuntimeService, ChannelRuntimeService>();
        return services;
    }
}
