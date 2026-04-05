using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public interface IIdeInstallerService
{
    Task<IdeInstallResult> InstallCompanionAsync(IdeInfo ide, CancellationToken cancellationToken = default);
}
