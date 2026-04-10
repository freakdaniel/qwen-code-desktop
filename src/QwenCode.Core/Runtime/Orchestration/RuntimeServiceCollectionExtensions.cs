using Microsoft.Extensions.DependencyInjection;
using QwenCode.Core.Runtime;
using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Provides extension members for Runtime Service Collection
/// </summary>
public static class RuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Executes add runtime services
    /// </summary>
    /// <param name="services">The services</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddRuntimeServices(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();
        services.AddSingleton<ProviderConfigurationResolver>();
        services.AddSingleton<IModelRegistry, ModelRegistryService>();
        services.AddSingleton<IModelConfigResolver, ModelConfigResolver>();
        services.AddSingleton<IBaseLlmClient, OpenAiCompatibleBaseLlmClient>();
        services.AddSingleton<IContentGenerator, ContentGenerator>();
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
