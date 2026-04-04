using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public static class SessionServiceCollectionExtensions
{
    public static IServiceCollection AddSessionServices(this IServiceCollection services)
    {
        services.AddSingleton<IChatCompressionService, ChatCompressionService>();
        services.AddSingleton<IChatRecordingService, ChatRecordingService>();
        services.AddSingleton<ITranscriptStore, DesktopSessionCatalogService>();
        services.AddSingleton<ISessionService>(static provider =>
            (DesktopSessionCatalogService)provider.GetRequiredService<ITranscriptStore>());
        services.AddSingleton<IInterruptedTurnStore, InterruptedTurnStore>();
        services.AddSingleton<IActiveTurnRegistry, ActiveTurnRegistry>();
        services.AddSingleton<ISessionTranscriptWriter, SessionTranscriptWriter>();
        services.AddSingleton<ISessionEventFactory, SessionEventFactory>();
        services.AddSingleton<IPendingApprovalResolver, PendingApprovalResolver>();
        services.AddSingleton<ISessionHost, DesktopSessionHostService>();

        return services;
    }
}
