using QwenCode.App.Models;

namespace QwenCode.App.Extensions;

public interface IExtensionCatalogService
{
    ExtensionSnapshot Inspect(WorkspacePaths paths);

    IReadOnlyList<CommandHookConfiguration> ListActiveHooks(WorkspacePaths paths);

    ExtensionSettingsSnapshot GetSettings(WorkspacePaths paths, GetExtensionSettingsRequest request);

    ExtensionSnapshot Install(WorkspacePaths paths, InstallExtensionRequest request);

    ExtensionConsentSnapshot PreviewConsent(WorkspacePaths paths, InstallExtensionRequest request);

    ExtensionScaffoldSnapshot CreateScaffold(WorkspacePaths paths, CreateExtensionScaffoldRequest request);

    ExtensionSnapshot Update(WorkspacePaths paths, UpdateExtensionRequest request);

    ExtensionSettingsSnapshot SetSetting(WorkspacePaths paths, SetExtensionSettingValueRequest request);

    ExtensionSnapshot SetEnabled(WorkspacePaths paths, SetExtensionEnabledRequest request);

    ExtensionSnapshot Remove(WorkspacePaths paths, RemoveExtensionRequest request);
}
