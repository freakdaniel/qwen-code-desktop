using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

/// <summary>
/// Provides extension members for Session Service Collection
/// </summary>
public static class SessionServiceCollectionExtensions
{
    /// <summary>
    /// Executes add session services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
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
        services.AddSingleton<PendingToolApprovalMessageHandler>();
        services.AddSingleton<PendingQuestionAnswerMessageHandler>();
        services.AddSingleton<ISessionMessageBus, SessionMessageBus>();
        services.AddSingleton<ISessionHost, DesktopSessionHostService>();

        return services;
    }
}
