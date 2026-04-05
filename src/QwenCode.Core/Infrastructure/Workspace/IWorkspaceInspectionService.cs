using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

/// <summary>
/// Defines the contract for Workspace Inspection Service
/// </summary>
public interface IWorkspaceInspectionService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting workspace snapshot</returns>
    WorkspaceSnapshot Inspect(WorkspacePaths paths);
}
