using QwenCode.App.Models;

namespace QwenCode.App.Extensions;

public interface IExtensionCatalogService
{
    ExtensionSnapshot Inspect(WorkspacePaths paths);

    ExtensionSettingsSnapshot GetSettings(WorkspacePaths paths, GetExtensionSettingsRequest request);

    ExtensionSnapshot Install(WorkspacePaths paths, InstallExtensionRequest request);

    ExtensionSettingsSnapshot SetSetting(WorkspacePaths paths, SetExtensionSettingValueRequest request);

    ExtensionSnapshot SetEnabled(WorkspacePaths paths, SetExtensionEnabledRequest request);

    ExtensionSnapshot Remove(WorkspacePaths paths, RemoveExtensionRequest request);
}
