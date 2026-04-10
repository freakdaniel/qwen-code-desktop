using QwenCode.Core.Models;

namespace QwenCode.Core.Infrastructure;

/// <summary>
/// Defines the contract for Git History Service
/// </summary>
public interface IGitHistoryService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting git history snapshot</returns>
    GitHistorySnapshot Inspect(WorkspacePaths paths);

    /// <summary>
    /// Creates checkpoint
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting git history snapshot</returns>
    GitHistorySnapshot CreateCheckpoint(WorkspacePaths paths, CreateGitCheckpointRequest request);

    /// <summary>
    /// Restores checkpoint
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting git history snapshot</returns>
    GitHistorySnapshot RestoreCheckpoint(WorkspacePaths paths, RestoreGitCheckpointRequest request);
}
