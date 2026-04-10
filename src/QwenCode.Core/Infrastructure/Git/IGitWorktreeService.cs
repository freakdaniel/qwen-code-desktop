using QwenCode.Core.Models;

namespace QwenCode.Core.Infrastructure;

/// <summary>
/// Defines the contract for Git Worktree Service
/// </summary>
public interface IGitWorktreeService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting git repository snapshot</returns>
    GitRepositorySnapshot Inspect(WorkspacePaths paths);

    /// <summary>
    /// Creates managed worktree
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting git repository snapshot</returns>
    GitRepositorySnapshot CreateManagedWorktree(WorkspacePaths paths, CreateManagedWorktreeRequest request);

    /// <summary>
    /// Cleans up managed session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting git repository snapshot</returns>
    GitRepositorySnapshot CleanupManagedSession(WorkspacePaths paths, CleanupManagedWorktreeSessionRequest request);

    /// <summary>
    /// Executes apply worktree changes
    /// </summary>
    /// <param name="sourceRepositoryPath">The source repository path</param>
    /// <param name="worktreePath">The worktree path</param>
    /// <returns>The resulting apply worktree changes result</returns>
    ApplyWorktreeChangesResult ApplyWorktreeChanges(string sourceRepositoryPath, string worktreePath);
}
