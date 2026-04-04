using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Agents;
using QwenCode.App.Auth;
using QwenCode.App.Channels;
using QwenCode.App.Compatibility;
using QwenCode.App.Config;
using QwenCode.App.Desktop;
using QwenCode.App.Extensions;
using QwenCode.App.Hooks;
using QwenCode.App.Ipc;
using QwenCode.App.Infrastructure;
using QwenCode.App.Mcp;
using QwenCode.App.Options;
using QwenCode.App.Permissions;
using QwenCode.App.Prompts;
using QwenCode.App.Runtime;
using QwenCode.App.Sessions;
using QwenCode.App.Tools;
using QwenCode.App.Followup;
using QwenCode.App.Output;
using QwenCode.App.Models;

namespace QwenCode.App.AppHost;

public static class DesktopShellServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopShellServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DesktopShellOptions>()
            .Bind(configuration.GetSection(DesktopShellOptions.SectionName));
        services.AddOptions<NativeAssistantRuntimeOptions>()
            .Bind(configuration.GetSection(NativeAssistantRuntimeOptions.SectionName));

        services
            .AddInfrastructureServices()
            .AddConfigServices()
            .AddAuthServices()
            .AddChannelServices()
            .AddCompatibilityServices()
            .AddExtensionServices()
            .AddHookServices()
            .AddPermissionServices()
            .AddRuntimeServices()
            .AddMcpServices()
            .AddPromptServices()
            .AddFollowupServices()
            .AddOutputServices()
            .AddAgentServices()
            .AddToolServices()
            .AddSessionServices()
            .AddDesktopServices()
            .AddAppHostServices();

        return services;
    }
}
