using Microsoft.Extensions.Options;
using QwenCode.App.Extensions;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Extension Projection Service
/// </summary>
/// <param name="options">The options</param>
/// <param name="workspacePathResolver">The workspace path resolver</param>
/// <param name="extensionCatalogService">The extension catalog service</param>
public sealed class ExtensionProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IExtensionCatalogService extensionCatalogService) : IDesktopExtensionProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    /// <summary>
    /// Creates snapshot
    /// </summary>
    /// <returns>The resulting extension snapshot</returns>
    public ExtensionSnapshot CreateSnapshot() =>
        extensionCatalogService.Inspect(ResolveWorkspace());

    /// <summary>
    /// Gets settings async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    public Task<ExtensionSettingsSnapshot> GetSettingsAsync(GetExtensionSettingsRequest request) =>
        Task.FromResult(extensionCatalogService.GetSettings(ResolveWorkspace(), request));

    /// <summary>
    /// Executes install async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    public Task<ExtensionSnapshot> InstallAsync(InstallExtensionRequest request) =>
        Task.FromResult(extensionCatalogService.Install(ResolveWorkspace(), request));

    /// <summary>
    /// Executes preview consent async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension consent snapshot</returns>
    public Task<ExtensionConsentSnapshot> PreviewConsentAsync(InstallExtensionRequest request) =>
        Task.FromResult(extensionCatalogService.PreviewConsent(ResolveWorkspace(), request));

    /// <summary>
    /// Creates scaffold async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension scaffold snapshot</returns>
    public Task<ExtensionScaffoldSnapshot> CreateScaffoldAsync(CreateExtensionScaffoldRequest request) =>
        Task.FromResult(extensionCatalogService.CreateScaffold(ResolveWorkspace(), request));

    /// <summary>
    /// Updates async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    public Task<ExtensionSnapshot> UpdateAsync(UpdateExtensionRequest request) =>
        Task.FromResult(extensionCatalogService.Update(ResolveWorkspace(), request));

    /// <summary>
    /// Sets setting async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    public Task<ExtensionSettingsSnapshot> SetSettingAsync(SetExtensionSettingValueRequest request) =>
        Task.FromResult(extensionCatalogService.SetSetting(ResolveWorkspace(), request));

    /// <summary>
    /// Sets enabled async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    public Task<ExtensionSnapshot> SetEnabledAsync(SetExtensionEnabledRequest request) =>
        Task.FromResult(extensionCatalogService.SetEnabled(ResolveWorkspace(), request));

    /// <summary>
    /// Removes async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    public Task<ExtensionSnapshot> RemoveAsync(RemoveExtensionRequest request) =>
        Task.FromResult(extensionCatalogService.Remove(ResolveWorkspace(), request));

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);
}
