using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

/// <summary>
/// Defines the contract for Workspace Path Resolver
/// </summary>
public interface IWorkspacePathResolver
{
    /// <summary>
    /// Resolves value
    /// </summary>
    /// <param name="configured">The configured</param>
    /// <returns>The resulting workspace paths</returns>
    WorkspacePaths Resolve(WorkspacePaths configured);
}
