using Microsoft.Extensions.DependencyInjection;

namespace QwenCode.App.Channels;

public static class ChannelServiceCollectionExtensions
{
    public static IServiceCollection AddChannelServices(this IServiceCollection services)
    {
        services.AddSingleton<IChannelRegistryService, ChannelRegistryService>();
        return services;
    }
}
