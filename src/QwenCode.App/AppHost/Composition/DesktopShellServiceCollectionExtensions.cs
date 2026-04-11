using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.Core.Agents;
using QwenCode.Core.Auth;
using QwenCode.Core.Channels;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Config;
using QwenCode.App.Desktop;
using QwenCode.App.Desktop.DirectConnect;
using QwenCode.Core.Extensions;
using QwenCode.Core.Hooks;
using QwenCode.Core.Ide;
using QwenCode.App.Ipc;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Mcp;
using QwenCode.App.Options;
using QwenCode.Core.Permissions;
using QwenCode.Core.Prompts;
using QwenCode.Core.Runtime;
using QwenCode.Core.Sessions;
using QwenCode.Core.Tools;
using QwenCode.Core.Telemetry;
using QwenCode.Core.Followup;
using QwenCode.Core.Output;
using QwenCode.Core.Models;

namespace QwenCode.App.AppHost;

/// <summary>
/// Provides extension members for Desktop Shell Service Collection
/// </summary>
public static class DesktopShellServiceCollectionExtensions
{
    /// <summary>
    /// Executes add desktop shell services
    /// </summary>
    /// <param name="services">The services</param>
    /// <param name="configuration">The configuration to apply</param>
    /// <returns>The resulting i service collection</returns>
    public static IServiceCollection AddDesktopShellServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DesktopShellOptions>()
            .Bind(configuration.GetSection(DesktopShellOptions.SectionName));
        services.AddOptions<NativeAssistantRuntimeOptions>()
            .Bind(configuration.GetSection(NativeAssistantRuntimeOptions.SectionName));
        services.AddOptions<DirectConnectServerOptions>()
            .Bind(configuration.GetSection(DirectConnectServerOptions.SectionName));

        services
            .AddInfrastructureServices()
            .AddConfigServices()
            .AddAuthServices()
            .AddChannelServices()
            .AddCompatibilityServices()
            .AddExtensionServices()
            .AddHookServices()
            .AddIdeServices()
            .AddPermissionServices()
            .AddTelemetryServices()
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
