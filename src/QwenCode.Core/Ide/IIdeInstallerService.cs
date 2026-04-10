using QwenCode.Core.Models;

namespace QwenCode.Core.Ide;

/// <summary>
/// Defines the contract for Ide Installer Service
/// </summary>
public interface IIdeInstallerService
{
    /// <summary>
    /// Executes install companion async
    /// </summary>
    /// <param name="ide">The ide</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide install result</returns>
    Task<IdeInstallResult> InstallCompanionAsync(IdeInfo ide, CancellationToken cancellationToken = default);
}
