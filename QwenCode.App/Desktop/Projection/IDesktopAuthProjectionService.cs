using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopAuthProjectionService
{
    event EventHandler<AuthStatusSnapshot>? AuthChanged;

    AuthStatusSnapshot CreateSnapshot();

    Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAsync(ConfigureOpenAiCompatibleAuthRequest request);

    Task<AuthStatusSnapshot> ConfigureCodingPlanAsync(ConfigureCodingPlanAuthRequest request);

    Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(ConfigureQwenOAuthRequest request);

    Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(StartQwenOAuthDeviceFlowRequest request);

    Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(CancelQwenOAuthDeviceFlowRequest request);

    Task<AuthStatusSnapshot> DisconnectAsync(DisconnectAuthRequest request);
}
