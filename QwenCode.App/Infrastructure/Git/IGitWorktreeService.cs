using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

public interface IGitWorktreeService
{
    GitRepositorySnapshot Inspect(WorkspacePaths paths);

    GitRepositorySnapshot CreateManagedWorktree(WorkspacePaths paths, CreateManagedWorktreeRequest request);

    GitRepositorySnapshot CleanupManagedSession(WorkspacePaths paths, CleanupManagedWorktreeSessionRequest request);

    ApplyWorktreeChangesResult ApplyWorktreeChanges(string sourceRepositoryPath, string worktreePath);
}
