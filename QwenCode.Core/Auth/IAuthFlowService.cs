using QwenCode.App.Models;

namespace QwenCode.App.Auth;

public interface IAuthFlowService
{
    event EventHandler<AuthStatusSnapshot>? AuthChanged;

    AuthStatusSnapshot GetStatus(WorkspacePaths paths);

    Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAsync(
        WorkspacePaths paths,
        ConfigureOpenAiCompatibleAuthRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthStatusSnapshot> ConfigureCodingPlanAsync(
        WorkspacePaths paths,
        ConfigureCodingPlanAuthRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(
        WorkspacePaths paths,
        ConfigureQwenOAuthRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(
        WorkspacePaths paths,
        StartQwenOAuthDeviceFlowRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(
        WorkspacePaths paths,
        CancelQwenOAuthDeviceFlowRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthStatusSnapshot> DisconnectAsync(
        WorkspacePaths paths,
        DisconnectAuthRequest request,
        CancellationToken cancellationToken = default);
}
