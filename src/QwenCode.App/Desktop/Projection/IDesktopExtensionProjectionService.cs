using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Extension Projection Service
/// </summary>
public interface IDesktopExtensionProjectionService
{
    /// <summary>
    /// Creates snapshot
    /// </summary>
    /// <returns>The resulting extension snapshot</returns>
    ExtensionSnapshot CreateSnapshot();

    /// <summary>
    /// Gets settings async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    Task<ExtensionSettingsSnapshot> GetSettingsAsync(GetExtensionSettingsRequest request);

    /// <summary>
    /// Executes install async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    Task<ExtensionSnapshot> InstallAsync(InstallExtensionRequest request);

    /// <summary>
    /// Executes preview consent async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension consent snapshot</returns>
    Task<ExtensionConsentSnapshot> PreviewConsentAsync(InstallExtensionRequest request);

    /// <summary>
    /// Creates scaffold async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension scaffold snapshot</returns>
    Task<ExtensionScaffoldSnapshot> CreateScaffoldAsync(CreateExtensionScaffoldRequest request);

    /// <summary>
    /// Updates async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    Task<ExtensionSnapshot> UpdateAsync(UpdateExtensionRequest request);

    /// <summary>
    /// Sets setting async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension settings snapshot</returns>
    Task<ExtensionSettingsSnapshot> SetSettingAsync(SetExtensionSettingValueRequest request);

    /// <summary>
    /// Sets enabled async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    Task<ExtensionSnapshot> SetEnabledAsync(SetExtensionEnabledRequest request);

    /// <summary>
    /// Removes async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to extension snapshot</returns>
    Task<ExtensionSnapshot> RemoveAsync(RemoveExtensionRequest request);
}
