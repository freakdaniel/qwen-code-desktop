using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

public interface IGitHistoryService
{
    GitHistorySnapshot Inspect(WorkspacePaths paths);

    GitHistorySnapshot CreateCheckpoint(WorkspacePaths paths, CreateGitCheckpointRequest request);

    GitHistorySnapshot RestoreCheckpoint(WorkspacePaths paths, RestoreGitCheckpointRequest request);
}
