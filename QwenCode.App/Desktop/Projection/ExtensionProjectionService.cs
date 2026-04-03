using Microsoft.Extensions.Options;
using QwenCode.App.Extensions;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

public sealed class ExtensionProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IExtensionCatalogService extensionCatalogService) : IDesktopExtensionProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    public ExtensionSnapshot CreateSnapshot() =>
        extensionCatalogService.Inspect(ResolveWorkspace());

    public Task<ExtensionSettingsSnapshot> GetSettingsAsync(GetExtensionSettingsRequest request) =>
        Task.FromResult(extensionCatalogService.GetSettings(ResolveWorkspace(), request));

    public Task<ExtensionSnapshot> InstallAsync(InstallExtensionRequest request) =>
        Task.FromResult(extensionCatalogService.Install(ResolveWorkspace(), request));

    public Task<ExtensionSettingsSnapshot> SetSettingAsync(SetExtensionSettingValueRequest request) =>
        Task.FromResult(extensionCatalogService.SetSetting(ResolveWorkspace(), request));

    public Task<ExtensionSnapshot> SetEnabledAsync(SetExtensionEnabledRequest request) =>
        Task.FromResult(extensionCatalogService.SetEnabled(ResolveWorkspace(), request));

    public Task<ExtensionSnapshot> RemoveAsync(RemoveExtensionRequest request) =>
        Task.FromResult(extensionCatalogService.Remove(ResolveWorkspace(), request));

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);
}
