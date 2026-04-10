using QwenCode.Core.Models;

namespace QwenCode.Core.Extensions;

/// <summary>
/// Defines the contract for Extension Catalog Service
/// </summary>
public interface IExtensionCatalogService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting extension snapshot</returns>
    ExtensionSnapshot Inspect(WorkspacePaths paths);

    /// <summary>
    /// Lists active hooks
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting i read only list command hook configuration</returns>
    IReadOnlyList<CommandHookConfiguration> ListActiveHooks(WorkspacePaths paths);

    /// <summary>
    /// Gets settings
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension settings snapshot</returns>
    ExtensionSettingsSnapshot GetSettings(WorkspacePaths paths, GetExtensionSettingsRequest request);

    /// <summary>
    /// Executes install
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension snapshot</returns>
    ExtensionSnapshot Install(WorkspacePaths paths, InstallExtensionRequest request);

    /// <summary>
    /// Executes preview consent
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension consent snapshot</returns>
    ExtensionConsentSnapshot PreviewConsent(WorkspacePaths paths, InstallExtensionRequest request);

    /// <summary>
    /// Creates scaffold
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension scaffold snapshot</returns>
    ExtensionScaffoldSnapshot CreateScaffold(WorkspacePaths paths, CreateExtensionScaffoldRequest request);

    /// <summary>
    /// Updates value
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension snapshot</returns>
    ExtensionSnapshot Update(WorkspacePaths paths, UpdateExtensionRequest request);

    /// <summary>
    /// Sets setting
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension settings snapshot</returns>
    ExtensionSettingsSnapshot SetSetting(WorkspacePaths paths, SetExtensionSettingValueRequest request);

    /// <summary>
    /// Sets enabled
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension snapshot</returns>
    ExtensionSnapshot SetEnabled(WorkspacePaths paths, SetExtensionEnabledRequest request);

    /// <summary>
    /// Removes value
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension snapshot</returns>
    ExtensionSnapshot Remove(WorkspacePaths paths, RemoveExtensionRequest request);
}
