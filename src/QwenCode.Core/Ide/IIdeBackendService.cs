using QwenCode.Core.Models;

namespace QwenCode.Core.Ide;

/// <summary>
/// Defines the contract for Ide Backend Service
/// </summary>
public interface IIdeBackendService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="workspaceRoot">The workspace root</param>
    /// <param name="processCommand">The process command</param>
    /// <returns>The resulting ide connection snapshot</returns>
    IdeConnectionSnapshot Inspect(string workspaceRoot, string processCommand = "");

    /// <summary>
    /// Resolves transport connection
    /// </summary>
    /// <param name="workspaceRoot">The workspace root</param>
    /// <param name="processCommand">The process command</param>
    /// <returns>The resulting ide transport connection info?</returns>
    IdeTransportConnectionInfo? ResolveTransportConnection(string workspaceRoot, string processCommand = "");

    /// <summary>
    /// Updates context
    /// </summary>
    /// <param name="snapshot">The snapshot</param>
    /// <returns>The resulting ide context snapshot</returns>
    IdeContextSnapshot UpdateContext(IdeContextSnapshot snapshot);

    /// <summary>
    /// Executes install companion async
    /// </summary>
    /// <param name="ide">The ide</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide install result</returns>
    Task<IdeInstallResult> InstallCompanionAsync(IdeInfo ide, CancellationToken cancellationToken = default);
}
