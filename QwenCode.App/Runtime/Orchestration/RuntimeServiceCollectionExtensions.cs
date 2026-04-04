using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Options;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public static class RuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeServices(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<ProviderConfigurationResolver>();
        services.AddSingleton<ILoopDetectionService, LoopDetectionService>();
        services.AddSingleton<ITokenLimitService, TokenLimitService>();
        services.AddSingleton<INonInteractiveToolExecutor, NonInteractiveToolExecutor>();
        services.AddSingleton<IToolCallScheduler, ToolCallScheduler>();
        services.AddSingleton<IAssistantPromptAssembler, AssistantPromptAssembler>();
        services.AddSingleton<ISlashCommandRuntime, SlashCommandRuntime>();
        services.AddSingleton<ICommandActionRuntime, CommandActionRuntime>();
        services.AddSingleton<IAssistantResponseProvider, DashScopeAssistantResponseProvider>();
        services.AddSingleton<IAssistantResponseProvider, OpenAiCompatibleAssistantResponseProvider>();
        services.AddSingleton<IAssistantResponseProvider, FallbackAssistantResponseProvider>();
        services.AddSingleton<IAssistantTurnRuntime, AssistantTurnRuntime>();

        return services;
    }
}
