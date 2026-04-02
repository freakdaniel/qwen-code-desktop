using Microsoft.Extensions.Options;
using QwenCode.App.Auth;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

public sealed class AuthProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IAuthFlowService authFlowService) : IDesktopAuthProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    public event EventHandler<AuthStatusSnapshot>? AuthChanged
    {
        add => authFlowService.AuthChanged += value;
        remove => authFlowService.AuthChanged -= value;
    }

    public AuthStatusSnapshot CreateSnapshot() => authFlowService.GetStatus(ResolveWorkspace());

    public Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAsync(ConfigureOpenAiCompatibleAuthRequest request) =>
        authFlowService.ConfigureOpenAiCompatibleAsync(ResolveWorkspace(), request);

    public Task<AuthStatusSnapshot> ConfigureCodingPlanAsync(ConfigureCodingPlanAuthRequest request) =>
        authFlowService.ConfigureCodingPlanAsync(ResolveWorkspace(), request);

    public Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(ConfigureQwenOAuthRequest request) =>
        authFlowService.ConfigureQwenOAuthAsync(ResolveWorkspace(), request);

    public Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(StartQwenOAuthDeviceFlowRequest request) =>
        authFlowService.StartQwenOAuthDeviceFlowAsync(ResolveWorkspace(), request);

    public Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(CancelQwenOAuthDeviceFlowRequest request) =>
        authFlowService.CancelQwenOAuthDeviceFlowAsync(ResolveWorkspace(), request);

    public Task<AuthStatusSnapshot> DisconnectAsync(DisconnectAuthRequest request) =>
        authFlowService.DisconnectAsync(ResolveWorkspace(), request);

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);
}
