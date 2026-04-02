using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface IToolExecutor
{
    NativeToolHostSnapshot Inspect(WorkspacePaths paths);

    Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        ExecuteNativeToolRequest request,
        CancellationToken cancellationToken = default);
}
