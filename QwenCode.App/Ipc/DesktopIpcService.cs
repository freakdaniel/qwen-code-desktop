using QwenCode.App.Attributes;
using QwenCode.App.Models;
using QwenCode.App.Services;

namespace QwenCode.App.Ipc;

public sealed class DesktopIpcService(
    IServiceProvider services,
    DesktopAppService desktopAppService) : IpcServiceBase(services)
{
    [IpcInvoke("qwen-desktop:app:bootstrap")]
    public Task<AppBootstrapPayload> Bootstrap()
        => desktopAppService.GetBootstrapAsync();

    [IpcInvoke("qwen-desktop:app:set-mode")]
    public Task<DesktopStateChangedEvent> SetMode(SetModeRequest request)
        => desktopAppService.SetModeAsync(request.Mode);

    [IpcInvoke("qwen-desktop:app:set-locale")]
    public Task<DesktopStateChangedEvent> SetLocale(SetLocaleRequest request)
        => desktopAppService.SetLocaleAsync(request.Locale);

    [IpcEvent("qwen-desktop:app:state-changed")]
    public void SubscribeStateChanged(Action<DesktopStateChangedEvent> emit)
    {
        desktopAppService.StateChanged += (_, state) => emit(state);
    }
}
