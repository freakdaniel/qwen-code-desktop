using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopExtensionProjectionService
{
    ExtensionSnapshot CreateSnapshot();

    Task<ExtensionSettingsSnapshot> GetSettingsAsync(GetExtensionSettingsRequest request);

    Task<ExtensionSnapshot> InstallAsync(InstallExtensionRequest request);

    Task<ExtensionConsentSnapshot> PreviewConsentAsync(InstallExtensionRequest request);

    Task<ExtensionScaffoldSnapshot> CreateScaffoldAsync(CreateExtensionScaffoldRequest request);

    Task<ExtensionSnapshot> UpdateAsync(UpdateExtensionRequest request);

    Task<ExtensionSettingsSnapshot> SetSettingAsync(SetExtensionSettingValueRequest request);

    Task<ExtensionSnapshot> SetEnabledAsync(SetExtensionEnabledRequest request);

    Task<ExtensionSnapshot> RemoveAsync(RemoveExtensionRequest request);
}
