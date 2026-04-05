using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Tools;

public interface IToolExecutor
{
    NativeToolHostSnapshot Inspect(WorkspacePaths paths);

    Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        ExecuteNativeToolRequest request,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default);
}
